using System.Buffers.Binary;
using PixelPointer.App.Linux;
using SkiaSharp;

namespace PixelPointer.UnitTests;

public sealed class ScreenCastTests
{
    [Theory]
    [InlineData(true, "sim")]
    [InlineData(false, "nao")]
    public void Timings_SeparatePortalRemoteAndFrameWithoutChangingLegacyFields(bool restoreRequested, string restoreStatus)
    {
        var message = ScreenCastCapture.FormatTimings(2, 3, 20830, 20832, 20840, 20954, restoreRequested);

        Assert.Equal($"ScreenCast: portal 20832 ms, primeiro quadro 122 ms (sessao 2 ms; fontes 1 ms; inicio 20827 ms; persistencia 2 ms; remoto 8 ms; leitura 114 ms; restore solicitado {restoreStatus})", message);
    }

    [Fact]
    public void Timings_HandleSubMillisecondStages()
    {
        var message = ScreenCastCapture.FormatTimings(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, false);

        Assert.Equal("ScreenCast: portal 0 ms, primeiro quadro 0 ms (sessao 0 ms; fontes 0 ms; inicio 0 ms; persistencia 0 ms; remoto 0 ms; leitura 0 ms; restore solicitado nao)", message);
    }

    [Fact]
    public void EnumFormat_IsAlignedVideoFormatObject()
    {
        var pod = SpaPod.BuildVideoEnumFormat();

        Assert.Equal(0, pod.Length % 8);
        Assert.Equal((uint)pod.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(pod));
        Assert.Equal(SpaPod.TypeObject, BinaryPrimitives.ReadUInt32LittleEndian(pod.AsSpan(4)));
        Assert.Equal(SpaPod.ObjectFormat, BinaryPrimitives.ReadUInt32LittleEndian(pod.AsSpan(8)));
        Assert.Equal(SpaPod.ParamEnumFormat, BinaryPrimitives.ReadUInt32LittleEndian(pod.AsSpan(12)));
        Assert.DoesNotContain(SpaPod.KeyVideoModifier, Keys(pod));
    }

    [Fact]
    public void ParseVideoFormat_ReadsDefaultsOfChoices() =>
        Assert.Equal(new SpaVideoInfo(SpaVideoFormat.BGRx, 1920, 1080), SpaPod.ParseVideoFormat(SpaPod.BuildVideoEnumFormat()));

    [Fact]
    public void ParseVideoFormat_ReadsNegotiatedPlainValues()
    {
        var pod = FormatPod(SpaVideoFormat.RGBA, 2560, 1440);

        Assert.Equal(new SpaVideoInfo(SpaVideoFormat.RGBA, 2560, 1440), SpaPod.ParseVideoFormat(pod));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(20)]
    public void ParseVideoFormat_RejectsTruncatedPods(int length)
    {
        var pod = FormatPod(SpaVideoFormat.BGRx, 800, 600);

        Assert.Null(SpaPod.ParseVideoFormat(pod.AsSpan(0, length)));
    }

    [Fact]
    public void ParseVideoFormat_RejectsUnsupportedFormatAndEmptySize()
    {
        Assert.Null(SpaPod.ParseVideoFormat(FormatPod((SpaVideoFormat)2, 800, 600)));
        Assert.Null(SpaPod.ParseVideoFormat(FormatPod(SpaVideoFormat.BGRx, 0, 600)));
    }

    [Fact]
    public void ParseVideoFormat_RejectsPropertyLargerThanObject()
    {
        var pod = FormatPod(SpaVideoFormat.BGRx, 800, 600);
        BinaryPrimitives.WriteUInt32LittleEndian(pod.AsSpan(24), 0xFFFF);

        Assert.Null(SpaPod.ParseVideoFormat(pod));
    }

    [Theory]
    [InlineData(8u, new byte[] { 0x30, 0x20, 0x10, 0x00 })]
    [InlineData(12u, new byte[] { 0x30, 0x20, 0x10, 0x80 })]
    [InlineData(7u, new byte[] { 0x10, 0x20, 0x30, 0x00 })]
    [InlineData(11u, new byte[] { 0x10, 0x20, 0x30, 0x7F })]
    public void Converter_ProducesOpaqueBgraWithStride(uint format, byte[] pixel)
    {
        // 2x2 com 4 bytes de preenchimento por linha.
        var data = new byte[12 * 2];
        for (var y = 0; y < 2; y++)
        {
            pixel.CopyTo(data, y * 12);
            pixel.CopyTo(data, (y * 12) + 4);
            data[(y * 12) + 8] = 0xEE;
        }

        using var bitmap = VideoFrameConverter.ToBitmap(data, new SpaVideoInfo((SpaVideoFormat)format, 2, 2), stride: 12);

        Assert.Equal(new SKColor(0x10, 0x20, 0x30, 0xFF), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(0x10, 0x20, 0x30, 0xFF), bitmap.GetPixel(1, 1));
    }

    [Fact]
    public void Converter_RejectsShortBuffers() =>
        Assert.Throws<InvalidOperationException>(() =>
            VideoFrameConverter.ToBitmap(new byte[15], new SpaVideoInfo(SpaVideoFormat.BGRx, 2, 2), stride: 8));

    [Fact]
    public void TokenStore_RoundTripsWithPrivatePermissions()
    {
        using var directory = new TemporaryDirectory();
        var store = new RestoreTokenStore(Path.Combine(directory.Path, "state", "pixelpointer", "token"));

        Assert.Null(store.Read());
        store.Write("abc-123");
        store.Write("def-456");

        Assert.Equal("def-456", store.Read());
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(store.Path)!));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(store.Path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(store.Path)!));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("com espaço")]
    [InlineData("linha\nnova")]
    [InlineData("acentuação")]
    public void TokenStore_RejectsInvalidTokens(string token)
    {
        using var directory = new TemporaryDirectory();
        var store = new RestoreTokenStore(Path.Combine(directory.Path, "token"));

        Assert.Throws<ArgumentException>(() => store.Write(token));
        Assert.False(File.Exists(store.Path));
    }

    [Fact]
    public void TokenStore_IgnoresMalformedAndOversizedFiles()
    {
        using var directory = new TemporaryDirectory();
        var store = new RestoreTokenStore(Path.Combine(directory.Path, "token"));

        File.WriteAllText(store.Path, "dois tokens");
        Assert.Null(store.Read());
        File.WriteAllText(store.Path, new string('a', RestoreTokenStore.MaxLength + 1));
        Assert.Null(store.Read());
    }

    [Fact]
    public void TokenStore_RefusesSymbolicLinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "alvo");
        File.WriteAllText(target, "conteudo-alheio");
        var store = new RestoreTokenStore(Path.Combine(directory.Path, "token"));
        File.CreateSymbolicLink(store.Path, target);

        Assert.Null(store.Read());
        Assert.Throws<IOException>(() => store.Write("novo"));
        Assert.Equal("conteudo-alheio", File.ReadAllText(target));
    }

    private static byte[] FormatPod(SpaVideoFormat format, uint width, uint height)
    {
        var body = new List<byte>();
        void Add(uint value) => body.AddRange(BitConverter.GetBytes(value));
        Add(SpaPod.ObjectFormat);
        Add(SpaPod.ParamFormat);
        foreach (var (key, value) in new[]
                 {
                     (SpaPod.KeyMediaType, SpaPod.MediaTypeVideo),
                     (SpaPod.KeyMediaSubtype, SpaPod.MediaSubtypeRaw),
                     (SpaPod.KeyVideoFormat, (uint)format),
                 })
        {
            Add(key);
            Add(0);
            Add(4);
            Add(SpaPod.TypeId);
            Add(value);
            Add(0);
        }

        Add(SpaPod.KeyVideoSize);
        Add(0);
        Add(8);
        Add(SpaPod.TypeRectangle);
        Add(width);
        Add(height);
        var pod = new List<byte>();
        pod.AddRange(BitConverter.GetBytes((uint)body.Count));
        pod.AddRange(BitConverter.GetBytes(SpaPod.TypeObject));
        pod.AddRange(body);
        return [.. pod];
    }

    private static List<uint> Keys(byte[] pod)
    {
        var keys = new List<uint>();
        var offset = 16;
        while (offset + 16 <= pod.Length)
        {
            keys.Add(BinaryPrimitives.ReadUInt32LittleEndian(pod.AsSpan(offset)));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(pod.AsSpan(offset + 8));
            offset += 16 + (((int)size + 7) & ~7);
        }

        return keys;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("pixelpointer-screencast-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
