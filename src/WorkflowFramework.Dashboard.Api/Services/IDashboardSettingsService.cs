namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Service for managing dashboard settings.
/// </summary>
public interface IDashboardSettingsService
{
    DashboardSettings Get();
    void Update(DashboardSettings settings);
}
