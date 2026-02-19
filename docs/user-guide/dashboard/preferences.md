---
uid: dashboard-preferences
title: Theme & Preferences
---

# Theme & Preferences

The Dashboard supports customizable themes and user preferences, all persisted in the browser's `localStorage`.

## Theme

### Toggling Themes

Click the **Theme** button in the toolbar to cycle through:

1. **Dark** (default) — dark backgrounds, light text
2. **Light** — light backgrounds, dark text
3. **System** — follows your OS preference

The current theme is stored in `localStorage` under the key `wf-theme`.

### CSS Custom Properties

Both themes are implemented via CSS custom properties, making it easy to customize:

```css
/* Dark theme (default) */
:root[data-theme="dark"] {
  --bg-primary: #1a1a2e;
  --bg-secondary: #16213e;
  --bg-surface: #0f3460;
  --text-primary: #e8e8e8;
  --text-secondary: #a8a8a8;
  --accent: #6366f1;
  --border: #2d2d44;
}

/* Light theme */
:root[data-theme="light"] {
  --bg-primary: #ffffff;
  --bg-secondary: #f8fafc;
  --bg-surface: #f1f5f9;
  --text-primary: #1e293b;
  --text-secondary: #64748b;
  --accent: #4f46e5;
  --border: #e2e8f0;
}
```

## User Preferences

Access preferences from the toolbar menu. All settings are stored in `localStorage`.

| Preference | Key | Default | Description |
|---|---|---|---|
| Show grid | `wf-grid` | `true` | Display grid lines on the canvas |
| Show minimap | `wf-minimap` | `true` | Display the minimap in the bottom-right |
| Auto-save interval | `wf-autosave` | `30` | Seconds between auto-saves (0 = disabled) |
| Default zoom | `wf-zoom` | `1.0` | Initial canvas zoom level |
| Sidebar state | `wf-sidebar` | `both` | Which sidebars are open: `left`, `right`, `both`, `none` |

> [!TIP]
> Preferences are per-browser. They won't sync across devices.

## Toast Notifications

The Dashboard shows toast notifications for important events:

| Type | Color | Examples |
|---|---|---|
| **Success** | Green | Workflow saved, run completed |
| **Error** | Red | Save failed, run error |
| **Warning** | Yellow | Validation warnings, connection lost |
| **Info** | Blue | Template applied, version restored |

Toasts appear in the **bottom-right** corner, stack vertically, and auto-dismiss after **5 seconds**. Click a toast to dismiss it early.
