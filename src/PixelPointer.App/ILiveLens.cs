namespace PixelPointer.App;

internal interface ILiveLens : IDisposable
{
    float Level { get; }

    void Suspend();

    void Resume();

    void Tick();
}
