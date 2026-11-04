using System.Runtime.InteropServices;
using PixelPointer.App.Capture;
using PixelPointer.App.Overlay;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>Captura da janela raiz com XGetImage (sessões X11).</summary>
internal sealed partial class X11ScreenCapture(DisplayBounds? region = null) : IScreenCapture
{
    private bool _loggedRegion;

    public string Name => region is null ? "X11 XGetImage" : "X11 XGetImage (monitor sob o cursor)";

    public Task<SKBitmap> CaptureAsync(CancellationToken cancellationToken)
    {
        var display = Xlib.XOpenDisplay(null);
        if (display == 0)
        {
            throw new InvalidOperationException("Não foi possível abrir o display X11.");
        }

        try
        {
            var root = Xlib.XDefaultRootWindow(display);
            var screen = Xlib.XDefaultScreen(display);
            var desktopWidth = Xlib.XDisplayWidth(display, screen);
            var desktopHeight = Xlib.XDisplayHeight(display, screen);
            var bounds = CaptureBounds(region, desktopWidth, desktopHeight);
            var width = bounds.Width;
            var height = bounds.Height;
            if (region is not null && !_loggedRegion)
            {
                DiagnosticLog.Write("RECORDING", $"source=XGetImage-region desktop={desktopWidth}x{desktopHeight} capture=({bounds.X},{bounds.Y},{width},{height})");
                _loggedRegion = true;
            }
            var image = Xlib.XGetImage(display, root, bounds.X, bounds.Y, (uint)width, (uint)height, ~0UL, Xlib.ZPixmap);
            if (image == 0)
            {
                throw new InvalidOperationException("XGetImage falhou.");
            }

            try
            {
                unsafe
                {
                    var header = (Xlib.XImage*)image;
                    if (header->bits_per_pixel != 32)
                    {
                        throw new NotSupportedException($"Profundidade de cor não suportada: {header->bits_per_pixel} bpp.");
                    }

                    var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
                    var dst = (byte*)bitmap.GetPixels();
                    for (var y = 0; y < height; y++)
                    {
                        CopyOpaquePixels(new ReadOnlySpan<byte>((byte*)header->data + (y * header->bytes_per_line), width * 4),
                            new Span<byte>(dst + (y * bitmap.RowBytes), width * 4));
                    }

                    return Task.FromResult(bitmap);
                }
            }
            finally
            {
                Xlib.XDestroyImage(image);
            }
        }
        finally
        {
            Xlib.XCloseDisplay(display);
        }
    }

    internal static DisplayBounds CaptureBounds(DisplayBounds? region, int desktopWidth, int desktopHeight)
    {
        var bounds = region ?? new DisplayBounds(0, 0, desktopWidth, desktopHeight);
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0
            || (long)bounds.X + bounds.Width > desktopWidth || (long)bounds.Y + bounds.Height > desktopHeight)
        {
            throw new InvalidOperationException("O monitor selecionado não corresponde à área de trabalho X11 atual.");
        }

        return bounds;
    }

    internal static void CopyOpaquePixels(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        source.CopyTo(destination);
        for (var offset = 3; offset < source.Length; offset += 4)
        {
            destination[offset] = byte.MaxValue;
        }
    }
}

internal static partial class Xlib
{
    private const string Lib = "libX11.so.6";
    internal const int ZPixmap = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct XErrorEvent
    {
        public int Type;
        public nint Display;
        public nuint ResourceId;
        public nuint Serial;
        public byte ErrorCode;
        public byte RequestCode;
        public byte MinorCode;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int XErrorHandler(nint display, nint errorEvent);

    [StructLayout(LayoutKind.Sequential)]
    internal struct XImage
    {
        public int width;
        public int height;
        public int xoffset;
        public int format;
        public nint data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
    }

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint XOpenDisplay(string? name);

    [LibraryImport(Lib)]
    internal static partial int XCloseDisplay(nint display);

    [LibraryImport(Lib)]
    internal static partial nint XDefaultRootWindow(nint display);

    [LibraryImport(Lib)]
    internal static partial int XQueryPointer(nint display, nint window, out nuint root, out nuint child,
        out int rootX, out int rootY, out int windowX, out int windowY, out uint mask);

    [LibraryImport(Lib)]
    internal static partial int XDefaultScreen(nint display);

    [LibraryImport(Lib)]
    internal static partial int XDisplayWidth(nint display, int screen);

    [LibraryImport(Lib)]
    internal static partial int XDisplayHeight(nint display, int screen);

    [LibraryImport(Lib)]
    internal static partial nint XGetImage(nint display, nint drawable, int x, int y, uint width, uint height, ulong planeMask, int format);

    [LibraryImport(Lib)]
    internal static partial int XDestroyImage(nint image);

    [LibraryImport(Lib)]
    internal static partial int XInitThreads();

    [LibraryImport(Lib)]
    internal static partial int XGrabKey(nint display, int keycode, uint modifiers, nint grabWindow, int ownerEvents, int pointerMode, int keyboardMode);

    [LibraryImport(Lib)]
    internal static partial int XKeysymToKeycode(nint display, nint keysym);

    [LibraryImport(Lib)]
    internal static partial int XNextEvent(nint display, nint eventBuffer);

    [LibraryImport(Lib)]
    internal static partial int XPeekEvent(nint display, nint eventBuffer);

    [LibraryImport(Lib)]
    internal static partial int XPending(nint display);

    [LibraryImport(Lib)]
    internal static partial int XSelectInput(nint display, nint window, long mask);

    [LibraryImport(Lib)]
    internal static partial int XUngrabKey(nint display, int keycode, uint modifiers, nint grabWindow);

    [LibraryImport(Lib)]
    internal static partial int XSync(nint display, int discard);

    [LibraryImport(Lib)]
    internal static partial nint XSetErrorHandler(nint handler);
}
