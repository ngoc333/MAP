-- Virtualized page loading for System > Menu Registration.
-- Keeps mes.map_page_list_f unchanged because it is still useful for page dropdown options.

DROP FUNCTION IF EXISTS mes.map_page_search_f(text, boolean, integer, integer);

CREATE OR REPLACE FUNCTION mes.map_page_search_f(
    p_search text DEFAULT NULL,
    p_include_inactive boolean DEFAULT true,
    p_skip integer DEFAULT 0,
    p_take integer DEFAULT 50)
RETURNS TABLE (
    page_id varchar,
    title_vi varchar,
    title_en varchar,
    icon varchar,
    assembly_name varchar,
    component_name varchar,
    is_active boolean,
    note text,
    upd_date timestamptz,
    total_count integer)
LANGUAGE sql STABLE AS $function$
    SELECT p.page_id,
           p.title_vi,
           p.title_en,
           p.icon,
           p.assembly_name,
           p.component_name,
           p.is_active,
           p.note,
           p.upd_date,
           count(*) OVER()::integer AS total_count
      FROM mes.map_page_t p
     WHERE (p_include_inactive OR p.is_active)
       AND (NULLIF(btrim(p_search), '') IS NULL OR
            concat_ws(' ', p.page_id, p.title_vi, p.title_en,
                      p.assembly_name, p.component_name)
                ILIKE '%' || btrim(p_search) || '%')
     ORDER BY p.page_id
    OFFSET GREATEST(COALESCE(p_skip, 0), 0)
     LIMIT GREATEST(COALESCE(p_take, 50), 1);
$function$;
