CREATE OR REPLACE FUNCTION mes.map_build_menu_f(
    p_program_id      varchar(100),
    p_parent_menu_id  varchar(100) DEFAULT NULL)
RETURNS jsonb
LANGUAGE sql
STABLE
AS $function$
    SELECT COALESCE(
        jsonb_agg(
            jsonb_build_object(
                'id', m.menu_id,
                'titles', jsonb_strip_nulls(
                    jsonb_build_object('vi', m.title_vi, 'en', m.title_en)),
                'icon', m.icon,
                'assembly', p.assembly_name,
                'component', p.component_name,
                'children', mes.map_build_menu_f(m.program_id, m.menu_id)
            ) ORDER BY m.sort_order, m.menu_id),
        '[]'::jsonb)
      FROM mes.map_program_menu_t m
      LEFT JOIN mes.map_page_t p
        ON p.page_id = m.page_id
       AND p.is_active
     WHERE m.program_id = p_program_id
       AND m.parent_menu_id IS NOT DISTINCT FROM p_parent_menu_id
       AND m.is_active;
$function$;

CREATE OR REPLACE FUNCTION mes.map_get_menu_f(
    p_program_id varchar(100) DEFAULT NULL)
RETURNS TABLE (map_get_menu_f text)
LANGUAGE plpgsql
STABLE
AS $function$
DECLARE
    v_program_id varchar(100) := NULLIF(btrim(p_program_id), '');
BEGIN
    IF v_program_id IS NULL THEN
        RETURN QUERY
        SELECT jsonb_build_object(
            'source', 'db',
            'menus', '[]'::jsonb)::text;
        RETURN;
    END IF;

    RETURN QUERY
    SELECT jsonb_build_object(
        'source', 'db',
        'programId', v_program_id,
        'menus', mes.map_build_menu_f(v_program_id, NULL))::text;
END;
$function$;
