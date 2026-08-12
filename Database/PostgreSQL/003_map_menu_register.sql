DROP FUNCTION IF EXISTS mes.map_page_list_f();

CREATE OR REPLACE FUNCTION mes.map_page_list_f(
    p_search text DEFAULT NULL,
    p_include_inactive boolean DEFAULT true)
RETURNS TABLE (
    page_id varchar, title_vi varchar, title_en varchar, icon varchar,
    assembly_name varchar, component_name varchar, is_active boolean, note text,
    add_date timestamptz, add_user varchar, add_ip varchar,
    upd_date timestamptz, upd_user varchar, upd_ip varchar)
LANGUAGE sql STABLE AS $function$
    SELECT p.page_id, p.title_vi, p.title_en, p.icon, p.assembly_name,
           p.component_name, p.is_active, p.note, p.add_date, p.add_user,
           p.add_ip, p.upd_date, p.upd_user, p.upd_ip
      FROM mes.map_page_t p
     WHERE (p_include_inactive OR p.is_active)
       AND (NULLIF(btrim(p_search), '') IS NULL OR
            concat_ws(' ', p.page_id, p.title_vi, p.title_en,
                      p.assembly_name, p.component_name) ILIKE '%' || btrim(p_search) || '%')
     ORDER BY p.page_id;
$function$;

CREATE OR REPLACE FUNCTION mes.map_program_list_f()
RETURNS TABLE (
    program_id varchar, start_page_id varchar, is_active boolean, note text,
    add_date timestamptz, add_user varchar, add_ip varchar,
    upd_date timestamptz, upd_user varchar, upd_ip varchar)
LANGUAGE sql STABLE AS $function$
    SELECT p.program_id, p.start_page_id, p.is_active, p.note, p.add_date,
           p.add_user, p.add_ip, p.upd_date, p.upd_user, p.upd_ip
      FROM mes.map_program_t p
     ORDER BY p.program_id;
$function$;

CREATE OR REPLACE FUNCTION mes.map_program_menu_list_f(p_program_id varchar(100))
RETURNS TABLE (
    program_id varchar, menu_id varchar, parent_menu_id varchar, page_id varchar,
    title_vi varchar, title_en varchar, icon varchar, sort_order integer,
    is_active boolean, note text)
LANGUAGE sql STABLE AS $function$
    SELECT m.program_id, m.menu_id, m.parent_menu_id, m.page_id, m.title_vi,
           m.title_en, m.icon, m.sort_order, m.is_active, m.note
      FROM mes.map_program_menu_t m
     WHERE m.program_id = upper(btrim(p_program_id))
     ORDER BY m.sort_order, m.menu_id;
$function$;

CREATE OR REPLACE PROCEDURE mes.map_page_save_p(
    p_rows jsonb, p_user_name text DEFAULT NULL, p_ip_address text DEFAULT NULL)
LANGUAGE plpgsql AS $procedure$
BEGIN
    IF jsonb_typeof(COALESCE(p_rows, '[]')) <> 'array' THEN
        RAISE EXCEPTION 'p_rows must be a JSON array';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(p_rows) e
               WHERE NULLIF(btrim(e.value->>'page_id'), '') IS NULL) THEN
        RAISE EXCEPTION 'PageId is required';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(p_rows) e
               WHERE NULLIF(btrim(e.value->>'title_vi'), '') IS NULL) THEN
        RAISE EXCEPTION 'TitleVi is required';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(p_rows) e
               WHERE NULLIF(btrim(e.value->>'assembly_name'), '') IS NULL) THEN
        RAISE EXCEPTION 'AssemblyName is required';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(p_rows) e
               WHERE NULLIF(btrim(e.value->>'component_name'), '') IS NULL) THEN
        RAISE EXCEPTION 'ComponentName is required';
    END IF;
    IF (SELECT count(*) FROM jsonb_array_elements(p_rows))
       <> (SELECT count(DISTINCT btrim(value->>'page_id')) FROM jsonb_array_elements(p_rows)) THEN
        RAISE EXCEPTION 'Duplicate PageId in payload';
    END IF;

    INSERT INTO mes.map_page_t(page_id, title_vi, title_en, icon, assembly_name,
                               component_name, is_active, note, add_user, add_ip)
    SELECT btrim(r.page_id), btrim(r.title_vi), NULLIF(r.title_en, ''), NULLIF(r.icon, ''),
           btrim(r.assembly_name), btrim(r.component_name), COALESCE(r.is_active, true),
           NULLIF(r.note, ''), p_user_name, p_ip_address
      FROM jsonb_to_recordset(p_rows) AS r(
          page_id text, title_vi text, title_en text, icon text,
          assembly_name text, component_name text, is_active boolean, note text)
    ON CONFLICT (page_id) DO UPDATE SET title_vi = EXCLUDED.title_vi,
        title_en = EXCLUDED.title_en, icon = EXCLUDED.icon,
        assembly_name = EXCLUDED.assembly_name, component_name = EXCLUDED.component_name,
        is_active = EXCLUDED.is_active, note = EXCLUDED.note, upd_date = now(),
        upd_user = p_user_name, upd_ip = p_ip_address;
END;
$procedure$;

CREATE OR REPLACE PROCEDURE mes.map_page_delete_p(p_page_id text)
LANGUAGE plpgsql AS $procedure$
BEGIN
    IF EXISTS (SELECT 1 FROM mes.map_program_t WHERE start_page_id = p_page_id)
       OR EXISTS (SELECT 1 FROM mes.map_program_menu_t WHERE page_id = p_page_id) THEN
        RAISE EXCEPTION 'Page % is referenced by a program or menu', p_page_id;
    END IF;
    DELETE FROM mes.map_page_t WHERE page_id = p_page_id;
END;
$procedure$;

CREATE OR REPLACE PROCEDURE mes.map_program_save_p(
    p_program_id text, p_start_page_id text, p_is_active boolean, p_note text,
    p_menu_items jsonb, p_user_name text DEFAULT NULL, p_ip_address text DEFAULT NULL)
LANGUAGE plpgsql AS $procedure$
DECLARE
    v_program_id text := NULLIF(upper(btrim(p_program_id)), '');
    v_start_page_id text := NULLIF(btrim(p_start_page_id), '');
    v_item jsonb;
    v_parent text;
    v_cursor text;
BEGIN
    IF v_program_id IS NULL THEN
        RAISE EXCEPTION 'ProgramId is required';
    END IF;
    IF v_start_page_id IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM mes.map_page_t WHERE page_id = v_start_page_id AND is_active) THEN
        RAISE EXCEPTION 'Startup page % does not exist or is inactive', v_start_page_id;
    END IF;
    IF jsonb_typeof(COALESCE(p_menu_items, '[]')) <> 'array' THEN
        RAISE EXCEPTION 'MenuItems must be a JSON array';
    END IF;
    IF (SELECT count(*) FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')))
       <> (SELECT count(DISTINCT value->>'menu_id') FROM jsonb_array_elements(COALESCE(p_menu_items, '[]'))) THEN
        RAISE EXCEPTION 'Duplicate MenuId in MenuItems';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i
               WHERE NULLIF(i->>'menu_id', '') IS NULL) THEN
        RAISE EXCEPTION 'MenuId is required';
    END IF;
    IF (SELECT count(*) FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i WHERE i->>'page_id' IS NOT NULL)
       <> (SELECT count(DISTINCT i->>'page_id') FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i WHERE i->>'page_id' IS NOT NULL) THEN
        RAISE EXCEPTION 'Duplicate PageId in MenuItems';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i
               WHERE i->>'page_id' IS NOT NULL AND i->>'menu_id' <> i->>'page_id') THEN
        RAISE EXCEPTION 'Page menu_id must equal page_id';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i
               WHERE i->>'page_id' IS NOT NULL AND NOT EXISTS
                     (SELECT 1 FROM mes.map_page_t p WHERE p.page_id = i->>'page_id' AND p.is_active)) THEN
        RAISE EXCEPTION 'Menu item references a missing or inactive page';
    END IF;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i
               WHERE i->>'parent_menu_id' IS NOT NULL AND NOT EXISTS
                     (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) j
                       WHERE j->>'menu_id' = i->>'parent_menu_id' AND j->>'page_id' IS NULL)) THEN
        RAISE EXCEPTION 'Menu parent must reference a group';
    END IF;
    FOR v_item IN SELECT value FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) LOOP
        v_parent := NULLIF(v_item->>'parent_menu_id', '');
        v_cursor := v_parent;
        WHILE v_cursor IS NOT NULL LOOP
            IF v_cursor = v_item->>'menu_id' THEN RAISE EXCEPTION 'Menu hierarchy contains a cycle'; END IF;
            SELECT NULLIF(value->>'parent_menu_id', '') INTO v_cursor
              FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) WHERE value->>'menu_id' = v_cursor;
        END LOOP;
    END LOOP;
    IF EXISTS (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) i
               WHERE i->>'page_id' IS NULL AND NOT EXISTS
                     (SELECT 1 FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) c
                       WHERE c->>'parent_menu_id' = i->>'menu_id' AND COALESCE((c->>'is_active')::boolean, true))) THEN
        RAISE EXCEPTION 'Group must contain an active child';
    END IF;

    INSERT INTO mes.map_program_t(program_id, start_page_id, is_active, note, add_user, add_ip)
    VALUES (v_program_id, v_start_page_id, COALESCE(p_is_active, true), p_note, p_user_name, p_ip_address)
    ON CONFLICT (program_id) DO UPDATE SET start_page_id = EXCLUDED.start_page_id,
        is_active = EXCLUDED.is_active, note = EXCLUDED.note, upd_date = now(),
        upd_user = p_user_name, upd_ip = p_ip_address;
    DELETE FROM mes.map_program_menu_t WHERE program_id = v_program_id;
    FOR v_item IN SELECT value FROM jsonb_array_elements(COALESCE(p_menu_items, '[]')) LOOP
        INSERT INTO mes.map_program_menu_t(program_id, menu_id, parent_menu_id, page_id,
            title_vi, title_en, icon, sort_order, is_active, note, add_user, add_ip)
        VALUES (v_program_id, v_item->>'menu_id', NULLIF(v_item->>'parent_menu_id', ''),
            NULLIF(v_item->>'page_id', ''), v_item->>'title_vi', v_item->>'title_en',
            v_item->>'icon', COALESCE((v_item->>'sort_order')::integer, 0),
            COALESCE((v_item->>'is_active')::boolean, true), v_item->>'note', p_user_name, p_ip_address);
    END LOOP;
END;
$procedure$;

-- Register the administration page without replacing existing page/menu data.
INSERT INTO mes.map_page_t
    (page_id, title_vi, title_en, icon, assembly_name, component_name, is_active, note)
VALUES
    ('system-menu-registration', 'Đăng ký menu', 'Menu registration', 'account_tree',
     'MAP.M.System.dll', 'MAP.M.System.Pages.MenuRegistrationPage', true,
     'MAP menu registration')
ON CONFLICT (page_id) DO NOTHING;

-- Add the page to the existing MAP custom menu without replacing user configuration.
INSERT INTO mes.map_program_menu_t
    (program_id, menu_id, parent_menu_id, page_id, sort_order, is_active, note)
SELECT pr.program_id,
       'system-menu-registration',
       'system',
       'system-menu-registration',
       COALESCE((SELECT MAX(m.sort_order)
                   FROM mes.map_program_menu_t m
                  WHERE m.program_id = pr.program_id
                    AND m.parent_menu_id = 'system'), 0) + 10,
       true,
       'MAP menu registration'
  FROM mes.map_program_t pr
 WHERE pr.program_id = 'MAP'
   AND pr.is_active = true
   AND EXISTS (SELECT 1
                 FROM mes.map_program_menu_t g
                WHERE g.program_id = pr.program_id
                  AND g.menu_id = 'system'
                  AND g.page_id IS NULL)
   AND EXISTS (SELECT 1
                 FROM mes.map_page_t p
                WHERE p.page_id = 'system-menu-registration'
                  AND p.is_active = true)
   AND NOT EXISTS (SELECT 1
                     FROM mes.map_program_menu_t m
                    WHERE m.program_id = pr.program_id
                      AND m.menu_id = 'system-menu-registration')
;
