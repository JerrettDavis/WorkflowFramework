using Microsoft.JSInterop;

namespace WorkflowFramework.Dashboard.Web.Services;

public sealed class UserPreferences
{
    public string Theme { get; set; } = "dark";
    public bool ShowGrid { get; set; } = true;
    public bool ShowMinimap { get; set; } = true;
    public int AutoSaveIntervalSeconds { get; set; } = 30;
    public double DefaultZoom { get; set; } = 1.0;
    public bool SidebarCollapsed { get; set; }
}

public sealed class UserPreferencesService(IJSRuntime js)
{
    private const string StorageKey = "wf-preferences";
    private UserPreferences? _cached;

    public async Task<UserPreferences> LoadAsync()
    {
        if (_cached is not null) return _cached;
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (json is not null)
                _cached = System.Text.Json.JsonSerializer.Deserialize<UserPreferences>(json);
        }
        catch { }
        _cached ??= new UserPreferences();
        return _cached;
    }

    public async Task SaveAsync(UserPreferences prefs)
    {
        _cached = prefs;
        var json = System.Text.Json.JsonSerializer.Serialize(prefs);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
