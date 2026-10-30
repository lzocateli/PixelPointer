using System.Runtime.InteropServices;
using System.Text;

namespace PixelPointer.App.MacOS;

internal static partial class ScreenshotPreferences
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint Utf8 = 0x08000100;

    internal static string? GetLocation()
    {
        var key = CFStringCreateWithCString(0, "location", Utf8);
        var domain = CFStringCreateWithCString(0, "com.apple.screencapture", Utf8);
        if (key == 0 || domain == 0)
        {
            if (key != 0)
            {
                CFRelease(key);
            }

            if (domain != 0)
            {
                CFRelease(domain);
            }

            throw new InvalidOperationException("Não foi possível consultar as preferências de captura do macOS.");
        }

        try
        {
            var value = CFPreferencesCopyAppValue(key, domain);
            if (value == 0)
            {
                return null;
            }

            try
            {
                var buffer = new byte[32768];
                if (CFGetTypeID(value) != CFStringGetTypeID()
                    || CFStringGetCString(value, buffer, buffer.Length, Utf8) == 0)
                {
                    throw new InvalidOperationException("A preferência de destino das capturas do macOS não é um caminho válido.");
                }

                return Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0));
            }
            finally
            {
                CFRelease(value);
            }
        }
        finally
        {
            CFRelease(key);
            CFRelease(domain);
        }
    }

    [LibraryImport(CoreFoundation, StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint CFStringCreateWithCString(nint allocator, string value, uint encoding);

    [LibraryImport(CoreFoundation)]
    private static partial nint CFPreferencesCopyAppValue(nint key, nint applicationId);

    [LibraryImport(CoreFoundation)]
    private static partial nuint CFGetTypeID(nint value);

    [LibraryImport(CoreFoundation)]
    private static partial nuint CFStringGetTypeID();

    [LibraryImport(CoreFoundation)]
    private static partial byte CFStringGetCString(nint value, [Out] byte[] buffer, nint bufferSize, uint encoding);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(nint value);
}
