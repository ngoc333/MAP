
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
    v_start_page mes.map_page_t%ROWTYPE;
    v_menus jsonb;
BEGIN
    IF v_program_id IS NULL THEN
        SELECT p.* INTO STRICT v_start_page
          FROM mes.map_page_t p
         WHERE p.page_id = 'system-config'
           AND p.is_active = true;

        v_menus := jsonb_build_array(jsonb_strip_nulls(jsonb_build_object(
            'id', v_start_page.page_id,
            'titles', jsonb_strip_nulls(jsonb_build_object(
                'vi', v_start_page.title_vi, 'en', v_start_page.title_en)),
            'icon', v_start_page.icon,
            'assembly', v_start_page.assembly_name,
            'component', v_start_page.component_name)));
    ELSE
        SELECT p.* INTO STRICT v_start_page
          FROM mes.map_program_t pr
          JOIN mes.map_page_t p ON p.page_id = pr.start_page_id
         WHERE pr.program_id = v_program_id
           AND pr.is_active = true
           AND p.is_active = true;

        IF EXISTS (SELECT 1 FROM mes.map_program_t WHERE program_id = v_program_id)
           AND NOT EXISTS (SELECT 1 FROM mes.map_program_t
                            WHERE program_id = v_program_id AND is_active = true) THEN
            RAISE EXCEPTION 'Program % is inactive', v_program_id;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM mes.map_program_t WHERE program_id = v_program_id) THEN
            RAISE EXCEPTION 'Program % does not exist', v_program_id;
        END IF;

        IF EXISTS (SELECT 1 FROM mes.map_program_menu_t WHERE program_id = v_program_id) THEN
            v_menus := mes.map_build_menu_f(v_program_id, NULL);
        ELSE
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
    END IF;

    RETURN QUERY SELECT jsonb_build_object(
        'startPageId', v_start_page.page_id,
        'startPage', jsonb_strip_nulls(jsonb_build_object(
            'id', v_start_page.page_id,
            'titles', jsonb_strip_nulls(jsonb_build_object(
                'vi', v_start_page.title_vi, 'en', v_start_page.title_en)),
            'icon', v_start_page.icon,
            'assembly', v_start_page.assembly_name,
            'component', v_start_page.component_name)),
        'menus', v_menus)::text;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        IF v_program_id IS NULL THEN
            RAISE EXCEPTION 'Active page system-config does not exist';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM mes.map_program_t WHERE program_id = v_program_id) THEN
            RAISE EXCEPTION 'Program % does not exist', v_program_id;
        END IF;
        RAISE EXCEPTION 'Program % has no active startup page', v_program_id;
END;
$function$;
