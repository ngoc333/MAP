using System.Text.Json;
using MAP.C.Contract.Database;
using MAP.M.System.Models.MenuRegistration;

namespace MAP.M.System.Services;

public sealed class MenuRegistrationDb(IDbApiClient client)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<List<PageRegistration>> PagesAsync(string dbName, string? search, bool includeInactive) =>
        FunctionAsync<PageRegistration>(dbName, "mes.map_page_list_f", new { p_search = search, p_include_inactive = includeInactive });

    public Task<List<ProgramRegistration>> ProgramsAsync(string dbName) =>
        FunctionAsync<ProgramRegistration>(dbName, "mes.map_program_list_f", new { });

    public Task<List<MenuRegistrationRow>> MenuAsync(string dbName, string programId) =>
        FunctionAsync<MenuRegistrationRow>(dbName, "mes.map_program_menu_list_f", new { p_program_id = programId });

    public Task SavePageAsync(string dbName, PageRegistration page, string? user, string? ip) =>
        ProcedureAsync(dbName, "mes.map_page_save_p", new
        {
            p_page_id = page.PageId, p_title_vi = page.TitleVi, p_title_en = page.TitleEn,
            p_icon = page.Icon, p_assembly_name = page.AssemblyName, p_component_name = page.ComponentName,
            p_is_active = page.IsActive, p_note = page.Note, p_user_name = user, p_ip_address = ip
        });

    public Task DeletePageAsync(string dbName, string pageId) =>
        ProcedureAsync(dbName, "mes.map_page_delete_p", new { p_page_id = pageId });

    public Task SaveProgramAsync(string dbName, ProgramRegistration program, IEnumerable<MenuRegistrationRow> rows,
        string? user, string? ip)
    {
        var menuItems = rows.Select(row => new
        {
            menu_id = row.PageId is null ? row.MenuId : row.PageId,
            parent_menu_id = row.ParentMenuId,
            page_id = row.PageId,
            title_vi = row.TitleVi,
            title_en = row.TitleEn,
            icon = row.Icon,
            sort_order = row.SortOrder,
            is_active = row.IsActive,
            note = row.Note
        });
        return ProcedureAsync(dbName, "mes.map_program_save_p", new
        {
            p_program_id = program.ProgramId, p_start_page_id = program.StartPageId,
            p_is_active = program.IsActive, p_note = program.Note,
            p_menu_items = menuItems, p_user_name = user, p_ip_address = ip
        });
    }

    private async Task<List<T>> FunctionAsync<T>(string dbName, string name, object parameters)
    {
        var response = await client.CallPostgreSqlFunctionAsync(dbName, name,
            JsonSerializer.SerializeToElement(parameters, JsonOptions));
        Validate(response);
        return ReadRows<T>(response, name);
    }

    private async Task ProcedureAsync(string dbName, string name, object parameters)
    {
        var response = await client.CallPostgreSqlProcedureAsync(dbName, name,
            JsonSerializer.SerializeToElement(parameters, JsonOptions));
        Validate(response);
    }

    private static List<T> ReadRows<T>(JsonElement response, string name)
    {
        if (!response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Database function {name} returned no rows.");
        return data.Deserialize<List<T>>(JsonOptions) ?? [];
    }

    private static void Validate(JsonElement response)
    {
        if (!response.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
            throw new InvalidOperationException(response.TryGetProperty("message", out var message)
                ? message.GetString() ?? "Database request failed." : "Database request failed.");
    }
}
