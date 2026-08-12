CREATE SCHEMA IF NOT EXISTS mes;

ALTER TABLE IF EXISTS mes.map_page RENAME TO map_page_t;
ALTER TABLE IF EXISTS mes.map_program RENAME TO map_program_t;
ALTER TABLE IF EXISTS mes.map_program_menu RENAME TO map_program_menu_t;

CREATE TABLE IF NOT EXISTS mes.map_page_t
(
    page_id         varchar(100) PRIMARY KEY,
    title_vi        varchar(200) NOT NULL,
    title_en        varchar(200),
    icon            varchar(100),
    assembly_name   varchar(255) NOT NULL,
    component_name  varchar(500) NOT NULL,
    is_active       boolean NOT NULL DEFAULT true,
    note            text,
    reserved_01     text,
    reserved_02     text,
    reserved_03     text,
    reserved_04     text,
    reserved_05     text,
    reserved_06     text,
    reserved_07     text,
    reserved_08     text,
    reserved_09     text,
    add_date        timestamptz NOT NULL DEFAULT now(),
    add_user        varchar(100),
    add_ip          varchar(50),
    upd_date        timestamptz,
    upd_user        varchar(100),
    upd_ip          varchar(50),
    CONSTRAINT ck_map_page_t_id CHECK (btrim(page_id) <> ''),
    CONSTRAINT ck_map_page_t_assembly CHECK (btrim(assembly_name) <> ''),
    CONSTRAINT ck_map_page_t_component CHECK (btrim(component_name) <> '')
);

CREATE TABLE IF NOT EXISTS mes.map_program_t
(
    program_id      varchar(100) PRIMARY KEY,
    start_page_id   varchar(100) NOT NULL
        REFERENCES mes.map_page_t(page_id),
    is_active       boolean NOT NULL DEFAULT true,
    note            text,
    reserved_01     text,
    reserved_02     text,
    reserved_03     text,
    reserved_04     text,
    reserved_05     text,
    reserved_06     text,
    reserved_07     text,
    reserved_08     text,
    reserved_09     text,
    add_date        timestamptz NOT NULL DEFAULT now(),
    add_user        varchar(100),
    add_ip          varchar(50),
    upd_date        timestamptz,
    upd_user        varchar(100),
    upd_ip          varchar(50),
    CONSTRAINT ck_map_program_t_id CHECK (btrim(program_id) <> '')
);

CREATE TABLE IF NOT EXISTS mes.map_program_menu_t
(
    program_id      varchar(100) NOT NULL
        REFERENCES mes.map_program_t(program_id) ON DELETE CASCADE,
    menu_id         varchar(100) NOT NULL,
    parent_menu_id  varchar(100),
    page_id         varchar(100)
        REFERENCES mes.map_page_t(page_id),
    title_vi        varchar(200),
    title_en        varchar(200),
    icon            varchar(100),
    sort_order      integer NOT NULL DEFAULT 0,
    is_active       boolean NOT NULL DEFAULT true,
    note            text,
    reserved_01     text,
    reserved_02     text,
    reserved_03     text,
    reserved_04     text,
    reserved_05     text,
    reserved_06     text,
    reserved_07     text,
    reserved_08     text,
    reserved_09     text,
    add_date        timestamptz NOT NULL DEFAULT now(),
    add_user        varchar(100),
    add_ip          varchar(50),
    upd_date        timestamptz,
    upd_user        varchar(100),
    upd_ip          varchar(50),
    PRIMARY KEY (program_id, menu_id),
    CONSTRAINT fk_map_program_menu_t_parent
        FOREIGN KEY (program_id, parent_menu_id)
        REFERENCES mes.map_program_menu_t(program_id, menu_id)
        DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT ck_map_program_menu_t_self_parent
        CHECK (parent_menu_id IS NULL OR parent_menu_id <> menu_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_map_program_menu_t_page
    ON mes.map_program_menu_t(program_id, page_id)
    WHERE page_id IS NOT NULL;
