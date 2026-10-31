using System.Buffers.Binary;

namespace PixelPointer.App.Linux;

/// <summary>Formatos de vídeo bruto SPA aceitos pela captura (spa/param/video/raw.h).</summary>
internal enum SpaVideoFormat : uint
{
    Unknown = 0,
    RGBx = 7,
    BGRx = 8,
    RGBA = 11,
    BGRA = 12,
}

internal readonly record struct SpaVideoInfo(SpaVideoFormat Format, int Width, int Height);

/// <summary>
/// Monta e lê os POD SPA usados na negociação do stream PipeWire. As funções de construção da libspa são inline
/// no C e não são exportadas, então o formato binário (spa/pod/pod.h) é escrito aqui.
/// </summary>
internal static class SpaPod
{
    internal const uint TypeId = 3;
    internal const uint TypeInt = 4;
    internal const uint TypeRectangle = 10;
    internal const uint TypeFraction = 11;
    internal const uint TypeObject = 15;
    internal const uint TypeChoice = 19;

    internal const uint ObjectFormat = 0x40003;
    internal const uint ParamEnumFormat = 3;
    internal const uint ParamFormat = 4;

    internal const uint KeyMediaType = 1;
    internal const uint KeyMediaSubtype = 2;
    internal const uint KeyVideoFormat = 0x20001;
    internal const uint KeyVideoModifier = 0x20002;
    internal const uint KeyVideoSize = 0x20003;
    internal const uint KeyVideoFramerate = 0x20004;

    internal const uint MediaTypeVideo = 2;
    internal const uint MediaSubtypeRaw = 1;

    private const uint ChoiceRange = 1;
    private const uint ChoiceEnum = 3;

    /// <summary>EnumFormat de vídeo bruto em memória: sem modificadores, para o compositor não usar DMA-BUF.</summary>
    internal static byte[] BuildVideoEnumFormat()
    {
        var body = new List<byte>();
        AppendUInt32(body, ObjectFormat);
        AppendUInt32(body, ParamEnumFormat);
        AppendProperty(body, KeyMediaType, Pod(TypeId, UInt32(MediaTypeVideo)));
        AppendProperty(body, KeyMediaSubtype, Pod(TypeId, UInt32(MediaSubtypeRaw)));
        AppendProperty(body, KeyVideoFormat, Choice(ChoiceEnum, TypeId, 4,
            UInt32((uint)SpaVideoFormat.BGRx), UInt32((uint)SpaVideoFormat.BGRx), UInt32((uint)SpaVideoFormat.BGRA),
            UInt32((uint)SpaVideoFormat.RGBx), UInt32((uint)SpaVideoFormat.RGBA)));
        AppendProperty(body, KeyVideoSize, Choice(ChoiceRange, TypeRectangle, 8,
            Pair(1920, 1080), Pair(1, 1), Pair(16384, 16384)));
        AppendProperty(body, KeyVideoFramerate, Choice(ChoiceRange, TypeFraction, 8,
            Pair(30, 1), Pair(0, 1), Pair(1000, 1)));
        return Pod(TypeObject, [.. body]);
    }

    /// <summary>Lê formato e dimensões de um POD Format negociado; null quando não é vídeo bruto suportado.</summary>
    internal static SpaVideoInfo? ParseVideoFormat(ReadOnlySpan<byte> pod)
    {
        if (pod.Length < 16 || ReadUInt32(pod, 4) != TypeObject)
        {
            return null;
        }

        var size = ReadUInt32(pod, 0);
        if (size > pod.Length - 8 || size < 8 || ReadUInt32(pod, 8) != ObjectFormat)
        {
            return null;
        }

        var body = pod.Slice(8, (int)size);
        uint? mediaType = null, mediaSubtype = null, format = null;
        int width = 0, height = 0;
        var offset = 8;
        while (offset + 16 <= body.Length)
        {
            var key = ReadUInt32(body, offset);
            var valueSize = ReadUInt32(body, offset + 8);
            var valueType = ReadUInt32(body, offset + 12);
            var valueStart = offset + 16;
            if (valueSize > body.Length - valueStart)
            {
                return null;
            }

            var value = body.Slice(valueStart, (int)valueSize);
            if (valueType == TypeChoice)
            {
                // Um Choice negociado traz o valor escolhido como primeiro elemento.
                if (value.Length < 16)
                {
                    return null;
                }

                valueType = ReadUInt32(value, 12);
                var elementSize = ReadUInt32(value, 8);
                if (elementSize > value.Length - 16)
                {
                    return null;
                }

                value = value.Slice(16, (int)elementSize);
            }

            switch (key)
            {
                case KeyMediaType when valueType == TypeId && value.Length >= 4:
                    mediaType = ReadUInt32(value, 0);
                    break;
                case KeyMediaSubtype when valueType == TypeId && value.Length >= 4:
                    mediaSubtype = ReadUInt32(value, 0);
                    break;
                case KeyVideoFormat when valueType == TypeId && value.Length >= 4:
                    format = ReadUInt32(value, 0);
                    break;
                case KeyVideoSize when valueType == TypeRectangle && value.Length >= 8:
                    width = (int)Math.Min(ReadUInt32(value, 0), int.MaxValue);
                    height = (int)Math.Min(ReadUInt32(value, 4), int.MaxValue);
                    break;
            }

            offset = valueStart + Align8((int)valueSize);
        }

        if (mediaType != MediaTypeVideo || mediaSubtype != MediaSubtypeRaw || width <= 0 || height <= 0
            || format is not ((uint)SpaVideoFormat.BGRx or (uint)SpaVideoFormat.BGRA
                or (uint)SpaVideoFormat.RGBx or (uint)SpaVideoFormat.RGBA))
        {
            return null;
        }

        return new SpaVideoInfo((SpaVideoFormat)format.Value, width, height);
    }

    private static void AppendProperty(List<byte> body, uint key, byte[] value)
    {
        AppendUInt32(body, key);
        AppendUInt32(body, 0);
        body.AddRange(value);
    }

    private static byte[] Choice(uint choiceType, uint childType, uint childSize, params byte[][] values)
    {
        var body = new List<byte>();
        AppendUInt32(body, choiceType);
        AppendUInt32(body, 0);
        AppendUInt32(body, childSize);
        AppendUInt32(body, childType);
        foreach (var value in values)
        {
            body.AddRange(value);
        }

        return Pod(TypeChoice, [.. body]);
    }

    private static byte[] Pod(uint type, byte[] body)
    {
        var result = new byte[8 + Align8(body.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), type);
        body.CopyTo(result, 8);
        return result;
    }

    private static byte[] UInt32(uint value)
    {
        var result = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, value);
        return result;
    }

    private static byte[] Pair(uint first, uint second) => [.. UInt32(first), .. UInt32(second)];

    private static void AppendUInt32(List<byte> target, uint value) => target.AddRange(UInt32(value));

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

    private static int Align8(int value) => (value + 7) & ~7;
}
