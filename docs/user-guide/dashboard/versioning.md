---
uid: dashboard-versioning
title: Versioning
---

# Versioning

Every workflow save is automatically versioned, giving you a complete history of changes with the ability to restore and compare versions.

> [!NOTE]
> For versioning of workflow definitions in code, see [Versioning](../versioning.md).

## Auto-Versioning

Each time you save a workflow (Ctrl+S), a new version is created automatically. You don't need to manage versions manually — just save and the history builds itself.

## Version History

Click the **Versions** button in the toolbar (or from the workflow's context menu) to view the full version history.

![Version history panel](../../images/dashboard/version-history.png)
*Version history showing all saves with timestamps and summaries.*

Each version entry shows:
- **Version number** — incremental (v1, v2, v3…)
- **Timestamp** — when the version was saved
- **Author** — who made the change
- **Change summary** — auto-generated description of what changed (e.g., "Added 2 steps, removed 1 connection")

## Restoring a Version

Click **Restore** on any version to revert the workflow to that point in time. Restoring:

- Sets the workflow definition back to the selected version's state
- Creates a **new version** (so the restore itself is versioned)
- Logs a `version_restore` entry in the [audit trail](audit.md)

> [!TIP]
> Restoring is non-destructive — you can always restore back to a later version if needed.

## Version Diff

Click **Diff** between any two versions to see what changed:

![Version diff](../../images/dashboard/version-diff.png)
*Diff view showing added, removed, and renamed steps between versions.*

The diff shows:
- **Added steps** — new steps in the newer version (green)
- **Removed steps** — steps deleted in the newer version (red)
- **Renamed steps** — steps with changed names (yellow)
- **Connection changes** — added or removed connections
