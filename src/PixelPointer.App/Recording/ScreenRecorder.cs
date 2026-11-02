using System.Diagnostics;
using PixelPointer.App.Capture;
using PixelPointer.Core.Recording;
using SkiaSharp;

namespace PixelPointer.App.Recording;

/// <summary>Grava a tela como GIF numa thread própria até <see cref="Stop"/> ou até o limite de duração.</summary>
internal sealed class ScreenRecorder
{
    internal const int FramesPerSecond = 8;
    internal const int MaxWidth = 1280;
    internal static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(5);

    private readonly IScreenCapture _capture;
    private readonly GifEncoder _encoder;
    private readonly FileStream _stream;
    private readonly CancellationTokenSource _stop = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Thread _thread;
    private SKBitmap? _pending;
    private TimeSpan _pendingAt;
    private Exception? _error;

    private ScreenRecorder(IScreenCapture capture, string path, FileStream stream, SKBitmap first)
    {
        _capture = capture;
        Path = path;
        _stream = stream;
        var (width, height) = ScaledSize(first.Width, first.Height, MaxWidth);
        _encoder = new GifEncoder(stream, width, height);
        _pending = Scale(first);
        _thread = new Thread(Record) { IsBackground = true, Name = "screen-recorder" };
        _thread.Start();
    }

    internal string Path { get; }

    internal static ScreenRecorder Start(IScreenCapture capture, string directory)
    {
        try
        {
            using var first = capture.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            var (path, stream) = SnapshotStorage.Create(directory, "gif");
            try
            {
                return new ScreenRecorder(capture, path, stream, first);
            }
            catch
            {
                stream.Dispose();
                File.Delete(path);
                throw;
            }
        }
        catch
        {
            (capture as IDisposable)?.Dispose();
            throw;
        }
    }

    /// <summary>Reduz proporcionalmente para no máximo <paramref name="maxWidth"/> pixels de largura.</summary>
    internal static (int Width, int Height) ScaledSize(int width, int height, int maxWidth)
    {
        if (width <= maxWidth)
        {
            return (width, height);
        }

        return (maxWidth, Math.Max(1, (int)Math.Round(height * (double)maxWidth / width)));
    }

    /// <summary>Encerra a gravação e devolve o caminho, os quadros e a duração; em falha o arquivo é removido.</summary>
    internal (string Path, int Frames, TimeSpan Duration) Stop()
    {
        _stop.Cancel();
        _thread.Join();
        _stop.Dispose();
        if (_error is not null)
        {
            File.Delete(Path);
            throw new InvalidOperationException($"A gravação falhou: {_error.Message}", _error);
        }

        return (Path, _encoder.FrameCount, _clock.Elapsed);
    }

    private void Record()
    {
        var interval = TimeSpan.FromSeconds(1.0 / FramesPerSecond);
        try
        {
            while (!_stop.IsCancellationRequested && _clock.Elapsed < MaxDuration)
            {
                var wait = _pendingAt + interval - _clock.Elapsed;
                if (wait > TimeSpan.Zero && _stop.Token.WaitHandle.WaitOne(wait))
                {
                    break;
                }

                using var frame = _capture.CaptureAsync(_stop.Token).GetAwaiter().GetResult();
                var now = _clock.Elapsed;
                WritePending(now);
                _pending = Scale(frame);
                _pendingAt = now;
            }

            WritePending(_clock.Elapsed);
            if (!_stop.IsCancellationRequested)
            {
                Console.Error.WriteLine($"Gravação atingiu {MaxDuration.TotalMinutes:0} min e foi encerrada; use o comando de gravação para salvar.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _error = ex;
        }
        catch (OperationCanceledException)
        {
            WritePending(_clock.Elapsed);
        }
        finally
        {
            _pending?.Dispose();
            _pending = null;
            try
            {
                _encoder.Finish();
            }
            catch (IOException ex)
            {
                _error ??= ex;
            }

            _stream.Dispose();
            // A sessão ScreenCast do Wayland só é encerrada aqui, ao fim da gravação.
            try
            {
                (_capture as IDisposable)?.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException or Tmds.DBus.Protocol.DBusErrorReplyException)
            {
                Console.Error.WriteLine($"aviso: não foi possível encerrar a captura ({ex.Message}).");
            }
        }
    }

    private void WritePending(TimeSpan now)
    {
        if (_pending is null)
        {
            return;
        }

        var centiseconds = (int)Math.Round((now - _pendingAt).TotalMilliseconds / 10);
        _encoder.AddFrame(_pending.GetPixelSpan(), _pending.RowBytes, centiseconds);
        _pending.Dispose();
        _pending = null;
    }

    private SKBitmap Scale(SKBitmap source) =>
        source.Resize(new SKImageInfo(_encoder.Width, _encoder.Height, SKColorType.Bgra8888, SKAlphaType.Opaque),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            ?? throw new InvalidOperationException("Não foi possível redimensionar o quadro.");
}
