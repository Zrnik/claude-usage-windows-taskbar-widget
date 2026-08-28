# Linux daemon operations

The AI Usage Widget daemon selects the first free local endpoint in
`127.0.0.1:43175-43195`. The plasmoid discovers the active daemon through its
`/health` response, so another application using one port in this range does
not prevent the widget from starting.

The Debian package enables the user service at login and reloads, resets, and
restarts it during package upgrades. An upgrade therefore replaces a failed or
older widget daemon automatically. It deliberately does not stop an unrelated
process that owns port 43175.

Before an in-widget update installs a package, it preserves the current daemon
settings in `~/.config/claude-usage-widget/settings.json.pre-update`. The active
`settings.json` is not modified by the package installer. When the installation
finishes, it restarts the daemon and requests Plasma to reload, so the widget
immediately runs the newly installed version.

## A port collision

When every port in the range is occupied, the daemon exits with status `78` and
systemd does not restart it. Resolve the collision before starting the service
again:

```bash
systemctl --user stop claude-usage-widget.service
systemctl --user reset-failed claude-usage-widget.service
ss -ltnp
```

Identify the processes using ports `43175` through `43195` and stop or
reconfigure enough of them to free one port. After a port is free, restore the
daemon:

```bash
systemctl --user start claude-usage-widget.service
systemctl --user status claude-usage-widget.service
```

## Existing core dumps

New core dumps are disabled for this service. Before removing old dumps, list
only entries belonging to this daemon and verify their timestamps and paths:

```bash
coredumpctl list ClaudeUsageWidget.LinuxDaemon
```

Remove only the verified daemon dump files through the host's coredump storage
administration procedure. Do not run a global coredump purge: it can delete
crash reports from unrelated applications.

## Restart protection

For failures other than a port collision, systemd waits 30 seconds between
restarts and permits at most three starts in ten minutes. If that limit is
reached, inspect the journal before manually starting the service again:

```bash
journalctl --user -u claude-usage-widget.service -b
```
