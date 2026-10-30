using System.Runtime.InteropServices;

namespace PixelPointer.App.Linux;

internal static partial class X11Capabilities
{
    private const string XlibName = "libX11.so.6";

    internal static bool HasCompositingManager()
    {
        var display = Xlib.XOpenDisplay(null);
        if (display == 0)
        {
            return false;
        }

        try
        {
            var screen = Xlib.XDefaultScreen(display);
            var selection = XInternAtom(display, $"_NET_WM_CM_S{screen}", 1);
            return selection != 0 && XGetSelectionOwner(display, selection) != 0;
        }
        finally
        {
            Xlib.XCloseDisplay(display);
        }
    }

    [LibraryImport(XlibName, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint XInternAtom(nint display, string atomName, int onlyIfExists);

    [LibraryImport(XlibName)]
    private static partial nint XGetSelectionOwner(nint display, nint atom);
}
