namespace PixelPointer.App;

internal enum SetupAction
{
    Close,
    Diagnose,
    RegisterDesktop,
    RequestCapture,
    StartDaemon,
}

internal readonly record struct SetupPolicy(bool Linux, bool Wayland, bool DaemonRunning)
{
    internal bool IsAvailable(SetupAction action) => action switch
    {
        SetupAction.Close or SetupAction.Diagnose => true,
        SetupAction.RegisterDesktop => Linux,
        SetupAction.RequestCapture => Linux && Wayland,
        SetupAction.StartDaemon => !DaemonRunning,
        _ => false,
    };
}
