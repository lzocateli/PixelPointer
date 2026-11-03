using System.Buffers.Binary;

namespace PixelPointer.App.Linux;

internal readonly record struct SpaCursorPosition(int X, int Y);

internal static class SpaCursorMetadata
{
    private const int CursorMetadataSize = 28;

    internal static bool TryParse(ReadOnlySpan<byte> data, out SpaCursorPosition position)
    {
        position = default;
        if (data.Length < CursorMetadataSize || BinaryPrimitives.ReadUInt32LittleEndian(data) == 0)
        {
            return false;
        }

        position = new SpaCursorPosition(BinaryPrimitives.ReadInt32LittleEndian(data[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(data[12..]));
        return true;
    }
}
