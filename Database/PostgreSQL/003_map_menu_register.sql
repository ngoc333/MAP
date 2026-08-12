CREATE OR REPLACE FUNCTION mes.map_page_list_f()
RETURNS TABLE
(
    page_id         varchar,
    title_vi        varchar,
    title_en        varchar,
    icon            varchar,
    assembly_name   varchar,
    component_name  varchar,
    is_active       boolean,
    note            text
)
LANGUAGE sql
STABLE
AS $function$
    SELECT page_id,
           title_vi,
           title_en,
           icon,
           assembly_name,
           component_name,
           is_active,
           note
      FROM mes.map_page_t
     ORDER BY page_id;
$function$;

CREATE OR REPLACE FUNCTION mes.map_program_list_f()
RETURNS TABLE
(
    program_id     varchar,
    start_page_id  varchar,
    is_active      boolean,
    note           text
)
LANGUAGE sql
STABLE
AS $function$
    SELECT program_id,
           start_page_id,
           is_active,
           note
      FROM mes.map_program_t
     ORDER BY program_id;
$function$;

CREATE OR REPLACE FUNCTION mes.map_program_menu_list_f(
    p_program_id varchar(100))
RETURNS TABLE
(
    program_id      varchar,
    menu_id         varchar,
    parent_menu_id  varchar,
    page_id         varchar,
    title_vi        varchar,
    title_en        varchar,
    icon            varchar,
    sort_order      integer,
    is_active       boolean,
    note            text
)
LANGUAGE sql
STABLE
AS $function$
    SELECT m.program_id,
           m.menu_id,
           m.parent_menu_id,
           m.page_id,
           m.title_vi,
           m.title_en,
           m.icon,
           m.sort_order,
           m.is_active,
           m.note
      FROM mes.map_program_menu_t m
     WHERE m.program_id = p_program_id
     ORDER BY m.sort_order, m.menu_id;
$function$;

CREATE OR REPLACE PROCEDURE mes.map_page_save_p(
    p_page_id         text,
    p_title_vi        text,
    p_title_en        text,
    p_icon            text,
    p_assembly_name   text,
    p_component_name  text,
    p_is_active       boolean,
    p_note            text,
    p_user_name       text DEFAULT NULL,
    p_ip_address      text DEFAULT NULL)
LANGUAGE plpgsql
AS $procedure$
BEGIN
    IF NULLIF(btrim(p_page_id), '') IS NULL
       OR NULLIF(btrim(p_assembly_name), '') IS NULL
       OR NULLIF(btrim(p_component_name), '') IS NULL THEN
        RAISE EXCEPTION 'PageId, AssemblyName and ComponentName are required';
    END IF;

    INSERT INTO mes.map_page_t
    (
        page_id,
        title_vi,
        title_en,
        icon,
        assembly_name,
        component_name,
        is_active,
        note,
        add_user,
        add_ip
    )
    VALUES
    (
        p_page_id,
        p_title_vi,
        p_title_en,
        p_icon,
        p_assembly_name,
        p_component_name,
        COALESCE(p_is_active, true),
        p_note,
        p_user_name,
        p_ip_address
    )
    ON CONFLICT (page_id) DO UPDATE
       SET title_vi       = EXCLUDED.title_vi,
           title_en       = EXCLUDED.title_en,
           icon           = EXCLUDED.icon,
           assembly_name  = EXCLUDED.assembly_name,
           component_name = EXCLUDED.component_name,
           is_active      = EXCLUDED.is_active,
           note           = EXCLUDED.note,
           upd_date       = now(),
           upd_user       = p_user_name,
           upd_ip         = p_ip_address;
END;
$procedure$;

CREATE OR REPLACE PROCEDURE mes.map_page_delete_p(p_page_id text)
LANGUAGE plpgsql
AS $procedure$
BEGIN
    IF EXISTS
    (
        SELECT 1
          FROM mes.map_program_t
         WHERE start_page_id = p_page_id
    )
    OR EXISTS
    (
        SELECT 1
          FROM mes.map_program_menu_t
         WHERE page_id = p_page_id
    ) THEN
        RAISE EXCEPTION 'Page % is referenced by a program or menu', p_page_id;
    END IF;

    DELETE FROM mes.map_page_t
     WHERE page_id = p_page_id;
END;
$procedure$;

CREATE OR REPLACE PROCEDURE mes.map_program_save_p(
    p_program_id     text,
    p_start_page_id  text,
    p_is_active      boolean,
    p_note           text,
    p_menu_items     jsonb,
    p_user_name      text DEFAULT NULL,
    p_ip_address     text DEFAULT NULL)
LANGUAGE plpgsql
AS $procedure$
DECLARE
    v_item jsonb;
BEGIN
    IF NULLIF(btrim(p_program_id), '') IS NULL
       OR NULLIF(btrim(p_start_page_id), '') IS NULL THEN
        RAISE EXCEPTION 'ProgramId and StartPageId are required';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
          FROM mes.map_page_t
         WHERE page_id = p_start_page_id
           AND is_active
    ) THEN
        RAISE EXCEPTION 'Startup page % does not exist or is inactive', p_start_page_id;
    END IF;

    IF jsonb_typeof(COALESCE(p_menu_items, '[]'::jsonb)) <> 'array' THEN
        RAISE EXCEPTION 'MenuItems must be a JSON array';
    END IF;

    IF
    (
        SELECT count(*)
          FROM jsonb_array_elements(COALESCE(p_menu_items, '[]'::jsonb))
    ) <>
    (
        SELECT count(DISTINCT value->>'menu_id')
          FROM jsonb_array_elements(COALESCE(p_menu_items, '[]'::jsonb))
    ) THEN
        RAISE EXCEPTION 'Duplicate MenuId in MenuItems';
    END IF;

    IF EXISTS
    (
        SELECT 1
          FROM jsonb_array_elements(COALESCE(p_menu_items, '[]'::jsonb)) i
         WHERE i->>'page_id' IS NOT NULL
           AND NOT EXISTS
           (
               SELECT 1
                 FROM mes.map_page_t p
                WHERE p.page_id = i->>'page_id'
                  AND p.is_active
           )
    ) THEN
        RAISE EXCEPTION 'Menu item references a missing or inactive page';
    END IF;

    INSERT INTO mes.map_program_t
    (
        program_id,
        start_page_id,
        is_active,
        note,
        add_user,
        add_ip
    )
    VALUES
    (
        p_program_id,
        p_start_page_id,
        COALESCE(p_is_active, true),
        p_note,
        p_user_name,
        p_ip_address
    )
    ON CONFLICT (program_id) DO UPDATE
       SET start_page_id = EXCLUDED.start_page_id,
           is_active     = EXCLUDED.is_active,
           note          = EXCLUDED.note,
           upd_date      = now(),
           upd_user      = p_user_name,
           upd_ip        = p_ip_address;

    DELETE FROM mes.map_program_menu_t
     WHERE program_id = p_program_id;

    FOR v_item IN
        SELECT value
          FROM jsonb_array_elements(COALESCE(p_menu_items, '[]'::jsonb))
    LOOP
        INSERT INTO mes.map_program_menu_t
        (
            program_id,
            menu_id,
            parent_menu_id,
            page_id,
            title_vi,
            title_en,
            icon,
            sort_order,
            is_active,
            note,
            add_user,
            add_ip
        )
        VALUES
        (
            p_program_id,
            v_item->>'menu_id',
            NULLIF(v_item->>'parent_menu_id', ''),
            NULLIF(v_item->>'page_id', ''),
            v_item->>'title_vi',
            v_item->>'title_en',
            v_item->>'icon',
            COALESCE((v_item->>'sort_order')::integer, 0),
            COALESCE((v_item->>'is_active')::boolean, true),
            v_item->>'note',
            p_user_name,
            p_ip_address
        );
    END LOOP;

    IF NOT EXISTS
    (
        SELECT 1
          FROM mes.map_program_menu_t
         WHERE program_id = p_program_id
           AND page_id = p_start_page_id
    ) THEN
        RAISE EXCEPTION 'Startup page must belong to the program menu';
    END IF;
END;
$procedure$;
