using PixelPointer.App.Hotkeys;

namespace PixelPointer.App.Linux;

/// <summary>Atalhos globais pelo portal org.freedesktop.portal.GlobalShortcuts (Wayland).</summary>
internal sealed class PortalHotkeys(PortalClient portal) : IHotkeyService
{
    private IDisposable? _watch;

    public string Name => "xdg-desktop-portal GlobalShortcuts";

    public async Task<string> StartAsync(Action<HotkeyCommand> onActivated, CancellationToken cancellationToken)
    {
        if (await portal.GetVersionAsync("org.freedesktop.portal.GlobalShortcuts").ConfigureAwait(false) == 0)
        {
            throw new NotSupportedException("O desktop não oferece o portal GlobalShortcuts; use 'pixelpointer zoom' num atalho do desktop.");
        }

        _watch = await portal.WatchShortcutActivatedAsync((id, token) => onActivated(new HotkeyCommand(id, token))).ConfigureAwait(false);
        var session = await portal.CreateShortcutSessionAsync(cancellationToken).ConfigureAwait(false);
        var bound = await portal.BindShortcutsAsync(session,
            [.. HotkeyDefaults.Shortcuts.Select(s => (s.Id, s.Description, s.Trigger))], cancellationToken).ConfigureAwait(false);
        return bound.Count == 0 ? "nenhum atalho vinculado" : string.Join(", ", bound.Select(b => $"{b.Id}={b.Trigger}"));
    }

    public void Dispose() => _watch?.Dispose();
}
