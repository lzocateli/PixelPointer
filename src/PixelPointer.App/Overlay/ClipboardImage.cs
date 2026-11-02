using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PixelPointer.App.Overlay;

/// <summary>Publica uma imagem PNG na área de transferência pelo SDL; os bytes ficam em memória nativa até o SDL liberá-los.</summary>
internal static unsafe partial class ClipboardImage
{
    internal const string MimeType = "image/png";
    private static readonly byte** MimeTypes = CreateMimeTypes();

    internal static void SetPng(ReadOnlySpan<byte> png)
    {
        if (png.IsEmpty)
        {
            throw new ArgumentException("A imagem está vazia.", nameof(png));
        }

        var block = (byte*)NativeMemory.Alloc((nuint)(sizeof(nuint) + png.Length));
        *(nuint*)block = (nuint)png.Length;
        png.CopyTo(new Span<byte>(block + sizeof(nuint), png.Length));
        // Mesmo em falha o SDL retém o bloco e chama Cleanup depois; liberar aqui causaria double free.
        if (SetClipboardData(&Provide, &Cleanup, (nint)block, MimeTypes, 1) == 0)
        {
            throw new InvalidOperationException(SDL.SDL3.SDL_GetError() ?? "SDL_SetClipboardData falhou.");
        }
    }

    private static byte** CreateMimeTypes()
    {
        var types = (byte**)NativeMemory.Alloc((nuint)sizeof(byte*));
        types[0] = (byte*)Marshal.StringToCoTaskMemUTF8(MimeType);
        return types;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void* Provide(nint userdata, byte* mimeType, nuint* size)
    {
        *size = *(nuint*)userdata;
        return (byte*)userdata + sizeof(nuint);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Cleanup(nint userdata) => NativeMemory.Free((void*)userdata);

    [LibraryImport("SDL3", EntryPoint = "SDL_SetClipboardData")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial byte SetClipboardData(delegate* unmanaged[Cdecl]<nint, byte*, nuint*, void*> callback,
        delegate* unmanaged[Cdecl]<nint, void> cleanup, nint userdata, byte** mimeTypes, nuint count);
}
