
CREATE OR REPLACE FUNCTION mes.map_get_menu_f(
    p_program_id varchar(100) DEFAULT NULL,
    p_ip_address text DEFAULT NULL,
    p_user_name  text DEFAULT NULL,
    p_local_path text DEFAULT NULL)
RETURNS TABLE (map_get_menu_f text)
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_program_id varchar(100) := NULLIF(upper(btrim(p_program_id)), '');
    v_start_page_id varchar(100);
    v_menus jsonb;
    v_node jsonb;
    v_children_map jsonb := '{}'::jsonb;
    v_root_nodes jsonb := '[]'::jsonb;
    v_rec record;
    v_parent_id text;
    v_child_array jsonb;
BEGIN
    -- ========================================================================
    -- BOOTSTRAP: ProgramId IS NULL -> return system-config page only
    -- ========================================================================
    IF v_program_id IS NULL THEN
        SELECT jsonb_build_array(jsonb_strip_nulls(jsonb_build_object(
            'id', p.page_id,
            'titles', jsonb_strip_nulls(jsonb_build_object(
                'vi', p.title_vi, 'en', p.title_en)),
            'icon', p.icon,
            'assembly', p.assembly_name,
            'component', p.component_name)))
        INTO v_menus
        FROM mes.map_page_t p
        WHERE p.page_id = 'system-config'
          AND p.is_active = true;

        IF v_menus IS NULL THEN
            RAISE EXCEPTION 'Active page system-config does not exist';
        END IF;

        RETURN QUERY SELECT jsonb_build_object(
            'menus', v_menus)::text;
        RETURN;
    END IF;

    -- ========================================================================
    -- NORMAL PROGRAM: validate program exists and is active
    -- ========================================================================
    IF NOT EXISTS (SELECT 1 FROM mes.map_program_t WHERE program_id = v_program_id) THEN
        RAISE EXCEPTION 'Program % does not exist', v_program_id;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM mes.map_program_t WHERE program_id = v_program_id AND is_active = true) THEN
        RAISE EXCEPTION 'Program % is inactive', v_program_id;
    END IF;

    -- Read nullable start_page_id
    SELECT pr.start_page_id INTO v_start_page_id
    FROM mes.map_program_t pr
    WHERE pr.program_id = v_program_id;

    -- Validate start_page_id only when present
    IF v_start_page_id IS NOT NULL THEN
        IF NOT EXISTS (
            SELECT 1 FROM mes.map_page_t p
            WHERE p.page_id = v_start_page_id AND p.is_active = true
        ) THEN
            RAISE EXCEPTION 'Startup page % does not exist or is inactive', v_start_page_id;
        END IF;
    END IF;

    -- ========================================================================
    -- DETERMINE MENU MODE: check if custom menu rows exist (any rows)
    -- ========================================================================
    IF EXISTS (SELECT 1 FROM mes.map_program_menu_t m WHERE m.program_id = v_program_id) THEN
        -- ====================================================================
        -- CUSTOM MENU MODE: build hierarchy inline
        -- ====================================================================

        -- Collect reachable active menu rows using recursive CTE
        -- Then build tree bottom-up in PL/pgSQL
        FOR v_rec IN
            WITH RECURSIVE menu_tree AS (
                -- Root nodes (no parent)
                SELECT m.menu_id, m.parent_menu_id, m.page_id,
                       m.title_vi, m.title_en, m.icon,
                       m.sort_order, m.is_active, m.menu_id AS root_menu_id,
                       0 AS depth
                FROM mes.map_program_menu_t m
                WHERE m.program_id = v_program_id
                  AND m.parent_menu_id IS NULL
                  AND m.is_active = true

                UNION ALL

                -- Children
                SELECT c.menu_id, c.parent_menu_id, c.page_id,
                       c.title_vi, c.title_en, c.icon,
                       c.sort_order, c.is_active, mt.root_menu_id,
                       mt.depth + 1
                FROM mes.map_program_menu_t c
                JOIN menu_tree mt ON mt.menu_id = c.parent_menu_id
                WHERE c.program_id = v_program_id
                  AND c.is_active = true
            )
            SELECT mt.menu_id, mt.parent_menu_id, mt.page_id,
                   mt.title_vi, mt.title_en, mt.icon,
                   mt.sort_order, mt.depth,
                   p.title_vi AS page_title_vi, p.title_en AS page_title_en,
                   p.icon AS page_icon,
                   p.assembly_name, p.component_name
            FROM menu_tree mt
            LEFT JOIN mes.map_page_t p ON p.page_id = mt.page_id AND p.is_active = true
            ORDER BY mt.depth DESC, mt.sort_order, mt.menu_id
        LOOP
            -- Build node JSON
            IF v_rec.page_id IS NOT NULL THEN
                -- Page node
                v_node := jsonb_strip_nulls(jsonb_build_object(
                    'id', v_rec.menu_id,
                    'titles', jsonb_strip_nulls(jsonb_build_object(
                        'vi', COALESCE(v_rec.title_vi, v_rec.page_title_vi),
                        'en', COALESCE(v_rec.title_en, v_rec.page_title_en))),
                    'icon', COALESCE(v_rec.icon, v_rec.page_icon),
                    'assembly', v_rec.assembly_name,
                    'component', v_rec.component_name));
            ELSE
                -- Group node: get accumulated children
                v_child_array := COALESCE(v_children_map->v_rec.menu_id, '[]'::jsonb);

                -- Skip empty groups
                IF jsonb_array_length(v_child_array) = 0 THEN
                    CONTINUE;
                END IF;

                v_node := jsonb_strip_nulls(jsonb_build_object(
                    'id', v_rec.menu_id,
                    'titles', jsonb_strip_nulls(jsonb_build_object(
                        'vi', v_rec.title_vi, 'en', v_rec.title_en)),
                    'icon', v_rec.icon,
                    'children', v_child_array));
            END IF;

            -- Attach node to parent or root
            IF v_rec.parent_menu_id IS NULL THEN
                v_root_nodes := v_root_nodes || v_node;
            ELSE
                v_children_map := jsonb_set(
                    v_children_map,
                    ARRAY[v_rec.parent_menu_id],
                    COALESCE(v_children_map->v_rec.parent_menu_id, '[]'::jsonb) || v_node,
                    true);
            END IF;
        END LOOP;

        -- Check if effective menu is empty
        IF jsonb_array_length(v_root_nodes) = 0 THEN
            RAISE EXCEPTION 'Program % has no active menu pages', v_program_id;
        END IF;

        v_menus := v_root_nodes;
    ELSE
        -- ====================================================================
        -- DEFAULT MENU MODE: all active pages as flat list
        -- ====================================================================
        SELECT COALESCE(jsonb_agg(jsonb_strip_nulls(jsonb_build_object(
                   'id', p.page_id,
                   'titles', jsonb_strip_nulls(jsonb_build_object(
                       'vi', p.title_vi, 'en', p.title_en)),
                   'icon', p.icon,
                   'assembly', p.assembly_name,
                   'component', p.component_name)) ORDER BY p.page_id), '[]'::jsonb)
        INTO v_menus
        FROM mes.map_page_t p
        WHERE p.is_active = true;
    END IF;

    -- ========================================================================
    -- BUILD FINAL JSON RESPONSE
    -- ========================================================================
    RETURN QUERY SELECT jsonb_strip_nulls(jsonb_build_object(
        'startPageId', v_start_page_id,
        'menus', v_menus))::text;
END;
$function$;
