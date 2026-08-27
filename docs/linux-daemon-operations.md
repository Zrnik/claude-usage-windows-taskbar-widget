# Linux daemon operations

The AI Usage Widget daemon owns the local HTTP endpoint `127.0.0.1:43175`. The
plasmoid expects this fixed endpoint, so do not assign that port to another
application.

The Debian package enables the user service at login and reloads, resets, and
restarts it during package upgrades. An upgrade therefore replaces a failed or
older widget daemon automatically. It deliberately does not stop an unrelated
process that owns port 43175.

## A port collision

When the daemon cannot bind the port, it exits with status `78` and systemd does
not restart it. Resolve the collision before starting the service again:

```bash
systemctl --user stop claude-usage-widget.service
systemctl --user reset-failed claude-usage-widget.service
ss -ltnp 'sport = :43175'
```

Identify the listed process and stop or reconfigure that other application. Do
not change the widget daemon's port: the plasmoid communicates with port 43175.
After the port is free, restore the daemon:

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
