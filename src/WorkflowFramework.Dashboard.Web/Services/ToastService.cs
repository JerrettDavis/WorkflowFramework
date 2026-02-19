namespace WorkflowFramework.Dashboard.Web.Services;

public enum ToastType { Success, Error, Warning, Info }

public sealed class ToastItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ToastType Type { get; set; }
    public string Message { get; set; } = "";
    public int DurationMs { get; set; } = 5000;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ToastService
{
    private readonly List<ToastItem> _toasts = [];
    public IReadOnlyList<ToastItem> Toasts => _toasts;
    public event Action? OnChange;

    public void Show(string message, ToastType type = ToastType.Info, int durationMs = 5000)
    {
        var toast = new ToastItem { Message = message, Type = type, DurationMs = durationMs };
        _toasts.Add(toast);
        OnChange?.Invoke();
    }

    public void Success(string message) => Show(message, ToastType.Success);
    public void Error(string message) => Show(message, ToastType.Error);
    public void Warning(string message) => Show(message, ToastType.Warning);
    public void Info(string message) => Show(message, ToastType.Info);

    public void Remove(string id)
    {
        _toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }
}
