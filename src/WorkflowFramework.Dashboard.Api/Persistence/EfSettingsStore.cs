using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Api.Persistence;

/// <summary>
/// EF Core settings store replacing the in-memory DashboardSettingsService.
/// Stores settings as per-user key-value pairs in the database.
/// </summary>
public sealed class EfSettingsStore : IDashboardSettingsService
{
    private readonly DashboardDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public const string DefaultUserId = "system";

    public EfSettingsStore(DashboardDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private string EffectiveUserId => _currentUser.UserId ?? DefaultUserId;

    public DashboardSettings Get() => Get(EffectiveUserId);

    public DashboardSettings Get(string userId)
    {
        var settings = new DashboardSettings();
        var entries = _db.UserSettings.Where(s => s.UserId == userId).ToList();
        foreach (var entry in entries)
            ApplySetting(settings, entry.Key, entry.Value);
        return settings;
    }

    public void Update(DashboardSettings settings) => Update(settings, EffectiveUserId);

    public void Update(DashboardSettings settings, string userId)
    {
        foreach (var (key, value) in SettingsToPairs(settings))
        {
            var existing = _db.UserSettings.FirstOrDefault(s => s.UserId == userId && s.Key == key);
            if (existing is not null)
                existing.Value = value;
            else
                _db.UserSettings.Add(new UserSettingEntity { UserId = userId, Key = key, Value = value });
        }
        _db.SaveChanges();
    }

    private static void ApplySetting(DashboardSettings settings, string key, string value)
    {
        switch (key)
        {
            case "OllamaUrl": settings.OllamaUrl = value; break;
            case "OpenAiApiKey": settings.OpenAiApiKey = value; break;
            case "AnthropicApiKey": settings.AnthropicApiKey = value; break;
            case "HuggingFaceApiKey": settings.HuggingFaceApiKey = value; break;
            case "OpenAiBaseUrl": settings.OpenAiBaseUrl = value; break;
            case "DefaultProvider": settings.DefaultProvider = value; break;
            case "DefaultModel": settings.DefaultModel = value; break;
            case "DefaultTimeoutSeconds" when int.TryParse(value, out var v): settings.DefaultTimeoutSeconds = v; break;
            case "MaxConcurrentRuns" when int.TryParse(value, out var v): settings.MaxConcurrentRuns = v; break;
        }
    }

    private static IEnumerable<(string Key, string Value)> SettingsToPairs(DashboardSettings s)
    {
        yield return ("OllamaUrl", s.OllamaUrl);
        if (s.OpenAiApiKey is not null) yield return ("OpenAiApiKey", s.OpenAiApiKey);
        if (s.AnthropicApiKey is not null) yield return ("AnthropicApiKey", s.AnthropicApiKey);
        if (s.HuggingFaceApiKey is not null) yield return ("HuggingFaceApiKey", s.HuggingFaceApiKey);
        if (s.OpenAiBaseUrl is not null) yield return ("OpenAiBaseUrl", s.OpenAiBaseUrl);
        if (s.DefaultProvider is not null) yield return ("DefaultProvider", s.DefaultProvider);
        if (s.DefaultModel is not null) yield return ("DefaultModel", s.DefaultModel);
        yield return ("DefaultTimeoutSeconds", s.DefaultTimeoutSeconds.ToString());
        yield return ("MaxConcurrentRuns", s.MaxConcurrentRuns.ToString());
    }
}
