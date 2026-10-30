using System.Runtime.InteropServices;
using PixelPointer.App.Capture;
using SkiaSharp;

namespace PixelPointer.App.Linux;

/// <summary>Captura da janela raiz com XGetImage (sessões X11).</summary>
internal sealed partial class X11ScreenCapture : IScreenCapture
{
    public string Name => "X11 XGetImage";

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
            var width = Xlib.XDisplayWidth(display, screen);
            var height = Xlib.XDisplayHeight(display, screen);
            var image = Xlib.XGetImage(display, root, 0, 0, (uint)width, (uint)height, ~0UL, Xlib.ZPixmap);
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
