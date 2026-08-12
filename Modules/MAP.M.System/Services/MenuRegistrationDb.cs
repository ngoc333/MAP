using MAP.C.Contract.Database;
using MAP.M.System.Models.MenuRegistration;

namespace MAP.M.System.Services;

public sealed class MenuRegistrationDb(IDbApiClient client)
{
    public Task<List<PageRegistration>> PagesAsync(string dbName, string? search, bool includeInactive) =>
        client.QueryPostgreSqlFunctionAsync<PageRegistration>(dbName, "mes.map_page_list_f",
            new { PSearch = search, PIncludeInactive = includeInactive });

    public Task<List<ProgramRegistration>> ProgramsAsync(string dbName) =>
        client.QueryPostgreSqlFunctionAsync<ProgramRegistration>(dbName, "mes.map_program_list_f", new { });

    public Task<List<MenuRegistrationRow>> MenuAsync(string dbName, string programId) =>
        client.QueryPostgreSqlFunctionAsync<MenuRegistrationRow>(dbName, "mes.map_program_menu_list_f",
            new { PProgramId = programId });

    public Task SavePagesAsync(string dbName, IEnumerable<PageRegistration> pages, string? user, string? ip) =>
        client.ExecutePostgreSqlProcedureAsync(dbName, "mes.map_page_save_p",
            new { PRows = pages, PUserName = user, PIpAddress = ip });

    public Task DeletePageAsync(string dbName, string pageId) =>
        client.ExecutePostgreSqlProcedureAsync(dbName, "mes.map_page_delete_p",
            new { PPageId = pageId });

    public Task SaveProgramAsync(string dbName, ProgramRegistration program, IEnumerable<MenuRegistrationRow> rows,
        string? user, string? ip)
    {
        var menuItems = rows.Select(row => new
        {
            MenuId = row.PageId is null ? row.MenuId : row.PageId,
            ParentMenuId = row.ParentMenuId,
            PageId = row.PageId,
            TitleVi = row.TitleVi,
            TitleEn = row.TitleEn,
            Icon = row.Icon,
            SortOrder = row.SortOrder,
            IsActive = row.IsActive,
            Note = row.Note
        });

        return client.ExecutePostgreSqlProcedureAsync(dbName, "mes.map_program_save_p", new
        {
            PProgramId = program.ProgramId,
            PStartPageId = program.StartPageId,
            PIsActive = program.IsActive,
            PNote = program.Note,
            PMenuItems = menuItems,
            PUserName = user,
            PIpAddress = ip
        });
    }
}
