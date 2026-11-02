using PixelPointer.App.Overlay;
using static SDL.SDL3;

namespace PixelPointer.UnitTests;

public sealed unsafe class ClipboardImageTests
{
    [Fact]
    public void SetPng_PublishesTheBytesUnderTheImageMimeType()
    {
        SDL_SetHint(SDL_HINT_VIDEO_DRIVER, "dummy");
        SDL_SetHint(SDL_HINT_NO_SIGNAL_HANDLERS, "1");
        Assert.True(SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO), SDL_GetError());
        try
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
            ClipboardImage.SetPng(png);
            // Uma segunda cópia libera a anterior pelo callback de limpeza.
            ClipboardImage.SetPng(png);

            Assert.True(SDL_HasClipboardData(ClipboardImage.MimeType));
            nuint size;
            var data = SDL_GetClipboardData(ClipboardImage.MimeType, &size);
            Assert.NotEqual(0, (nint)data);
            try
            {
                Assert.Equal(png, new ReadOnlySpan<byte>((void*)data, (int)size).ToArray());
            }
            finally
            {
                SDL_free(data);
            }
        }
        finally
        {
            SDL_Quit();
        }
    }

    [Fact]
    public void SetPng_RejectsEmptyImage() =>
        Assert.Throws<ArgumentException>(() => ClipboardImage.SetPng([]));
}
