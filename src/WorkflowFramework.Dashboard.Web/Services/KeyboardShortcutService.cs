using Microsoft.JSInterop;

namespace WorkflowFramework.Dashboard.Web.Services;

public sealed class KeyboardShortcutService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<KeyboardShortcutService>? _dotNetRef;
    private readonly Dictionary<string, Func<Task>> _handlers = new();

    public event Func<Task>? OnShowHelp;

    public KeyboardShortcutService(IJSRuntime js) => _js = js;

    public void Register(string shortcut, Func<Task> handler) => _handlers[shortcut] = handler;
    public void Unregister(string shortcut) => _handlers.Remove(shortcut);

    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("keyboardShortcuts.initialize", _dotNetRef);
    }

    [JSInvokable]
    public async Task HandleShortcut(string shortcut)
    {
        if (shortcut is "?" or "F1")
        {
            if (OnShowHelp is not null)
                await OnShowHelp.Invoke();
            return;
        }
        if (_handlers.TryGetValue(shortcut, out var handler))
            await handler();
    }

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("keyboardShortcuts.destroy"); } catch { }
        _dotNetRef?.Dispose();
    }

    public static IReadOnlyList<(string Shortcut, string Description)> AllShortcuts =>
    [
        ("Ctrl+S", "Save workflow"),
        ("Ctrl+Shift+S", "Save As"),
        ("Ctrl+N", "New workflow"),
        ("Ctrl+O", "Open workflow"),
        ("Ctrl+Z", "Undo"),
        ("Ctrl+Shift+Z", "Redo"),
        ("Ctrl+Y", "Redo"),
        ("Ctrl+C", "Copy nodes"),
        ("Ctrl+V", "Paste nodes"),
        ("Delete", "Delete selected"),
        ("Backspace", "Delete selected"),
        ("Ctrl+A", "Select all nodes"),
        ("Ctrl+D", "Duplicate selected"),
        ("Ctrl+Enter", "Run workflow"),
        ("Escape", "Deselect / close dialogs"),
        ("?", "Show keyboard shortcuts"),
        ("F1", "Show keyboard shortcuts")
    ];
}
