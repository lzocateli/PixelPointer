namespace PixelPointer.App.Hotkeys;

/// <summary>Comando acionado por atalho global, CLI ou IPC.</summary>
internal sealed record HotkeyCommand(string Id, string? ActivationToken = null);

internal interface IHotkeyService : IDisposable
{
    string Name { get; }

    /// <summary>Registra Ctrl+1 (zoom), Ctrl+2 (draw), Ctrl+3 (timer) e Ctrl+6 (snip) e devolve o que ficou vinculado.</summary>
    Task<string> StartAsync(Action<HotkeyCommand> onActivated, CancellationToken cancellationToken);
}

internal static class HotkeyDefaults
{
    internal static readonly IReadOnlyList<(string Id, string Description, string Trigger, char Key)> Shortcuts =
    [
        ("zoom", "Zoom", "CTRL+1", '1'),
        ("draw", "Desenhar", "CTRL+2", '2'),
        ("timer", "Timer", "CTRL+3", '3'),
        ("snip", "Recorte", "CTRL+6", '6'),
    ];
}

internal static class HotkeyStartup
{
    internal static bool IsRecoverable(Exception error) =>
        error is NotSupportedException or InvalidOperationException or OperationCanceledException
            or Tmds.DBus.Protocol.DBusErrorReplyException;
}
