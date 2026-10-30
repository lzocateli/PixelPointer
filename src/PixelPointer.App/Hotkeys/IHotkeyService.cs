namespace PixelPointer.App.Hotkeys;

/// <summary>Comando acionado por atalho global, CLI ou IPC.</summary>
internal sealed record HotkeyCommand(string Id, string? ActivationToken = null);

internal interface IHotkeyService : IDisposable
{
    string Name { get; }

    /// <summary>Registra Ctrl+1 (zoom) e Ctrl+2 (draw) e devolve uma descrição do que ficou vinculado.</summary>
    Task<string> StartAsync(Action<HotkeyCommand> onActivated, CancellationToken cancellationToken);
}

internal static class HotkeyDefaults
{
    internal static readonly IReadOnlyList<(string Id, string Description, string Trigger, char Key)> Shortcuts =
    [
        ("zoom", "Zoom", "CTRL+1", '1'),
        ("draw", "Desenhar", "CTRL+2", '2'),
    ];
}
