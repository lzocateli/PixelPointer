namespace PixelPointer.Core.Recording;

/// <summary>
/// Codificador GIF89a em fluxo, sem dependências: paleta fixa de 252 cores (6×7×6), quadros inteiros e LZW.
/// Repete em laço infinito, como uma demonstração gravada.
/// </summary>
public sealed class GifEncoder
{
    private const int ColorBits = 8;
    private const int MaxCode = 4095;
    private static readonly byte[] Palette = BuildPalette();

    private readonly Stream _output;
    private bool _finished;

    public GifEncoder(Stream output, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (width is <= 0 or > ushort.MaxValue || height is <= 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensões fora do intervalo do GIF.");
        }

        _output = output;
        Width = width;
        Height = height;
        Write("GIF89a"u8);
        WriteUInt16(width);
        WriteUInt16(height);
        // Tabela global de 256 cores (2^(7+1)), resolução de 8 bits.
        _output.WriteByte(0xF7);
        _output.WriteByte(0);
        _output.WriteByte(0);
        _output.Write(Palette);
        Write([0x21, 0xFF, 0x0B]);
        Write("NETSCAPE2.0"u8);
        Write([0x03, 0x01, 0x00, 0x00, 0x00]);
    }

    public int Width { get; }

    public int Height { get; }

    public int FrameCount { get; private set; }

    /// <summary>Índice da paleta mais próximo de uma cor BGRA.</summary>
    public static byte Quantize(byte blue, byte green, byte red) =>
        (byte)((red * 6 / 256 * 42) + (green * 7 / 256 * 6) + (blue * 6 / 256));

    /// <summary>Acrescenta um quadro BGRA com <paramref name="stride"/> bytes por linha, exibido por <paramref name="delayCentiseconds"/>.</summary>
    public void AddFrame(ReadOnlySpan<byte> bgra, int stride, int delayCentiseconds)
    {
        ObjectDisposedException.ThrowIf(_finished, this);
        if (stride < Width * 4 || bgra.Length < (stride * (Height - 1)) + (Width * 4))
        {
            throw new ArgumentException("O quadro é menor que as dimensões do GIF.", nameof(bgra));
        }

        var indices = new byte[Width * Height];
        for (var y = 0; y < Height; y++)
        {
            var row = bgra.Slice(y * stride, Width * 4);
            for (var x = 0; x < Width; x++)
            {
                indices[(y * Width) + x] = Quantize(row[x * 4], row[(x * 4) + 1], row[(x * 4) + 2]);
            }
        }

        var delay = Math.Clamp(delayCentiseconds, 2, ushort.MaxValue);
        Write([0x21, 0xF9, 0x04, 0x00]);
        WriteUInt16(delay);
        Write([0x00, 0x00, 0x2C]);
        WriteUInt16(0);
        WriteUInt16(0);
        WriteUInt16(Width);
        WriteUInt16(Height);
        _output.WriteByte(0);
        _output.WriteByte(ColorBits);
        WriteLzw(indices);
        _output.WriteByte(0);
        FrameCount++;
    }

    public void Finish()
    {
        if (_finished)
        {
            return;
        }

        _output.WriteByte(0x3B);
        _output.Flush();
        _finished = true;
    }

    private void WriteLzw(byte[] indices)
    {
        const int clear = 1 << ColorBits;
        const int end = clear + 1;
        var blocks = new SubBlockWriter(_output);
        var codes = new Dictionary<int, int>(MaxCode);
        var codeSize = ColorBits + 1;
        var next = end + 1;
        blocks.WriteCode(clear, codeSize);
        var prefix = (int)indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            var symbol = indices[i];
            var key = (prefix << 8) | symbol;
            if (codes.TryGetValue(key, out var code))
            {
                prefix = code;
                continue;
            }

            blocks.WriteCode(prefix, codeSize);
            if (next <= MaxCode)
            {
                codes[key] = next++;
                // O decodificador amplia o código um passo depois; o tamanho acompanha o próximo código a emitir.
                if (next - 1 == 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }
            else
            {
                blocks.WriteCode(clear, codeSize);
                codes.Clear();
                codeSize = ColorBits + 1;
                next = end + 1;
            }

            prefix = symbol;
        }

        blocks.WriteCode(prefix, codeSize);
        blocks.WriteCode(end, codeSize);
        blocks.Flush();
    }

    private static byte[] BuildPalette()
    {
        var palette = new byte[256 * 3];
        for (var r = 0; r < 6; r++)
        {
            for (var g = 0; g < 7; g++)
            {
                for (var b = 0; b < 6; b++)
                {
                    var index = ((r * 42) + (g * 6) + b) * 3;
                    palette[index] = (byte)(r * 255 / 5);
                    palette[index + 1] = (byte)(g * 255 / 6);
                    palette[index + 2] = (byte)(b * 255 / 5);
                }
            }
        }

        return palette;
    }

    private void Write(ReadOnlySpan<byte> bytes) => _output.Write(bytes);

    private void WriteUInt16(int value)
    {
        _output.WriteByte((byte)value);
        _output.WriteByte((byte)(value >> 8));
    }

    /// <summary>Empacota códigos LSB-first em sub-blocos de até 255 bytes.</summary>
    private sealed class SubBlockWriter(Stream output)
    {
        private readonly byte[] _block = new byte[255];
        private int _length;
        private int _bits;
        private int _bitCount;

        public void WriteCode(int code, int size)
        {
            _bits |= code << _bitCount;
            _bitCount += size;
            while (_bitCount >= 8)
            {
                Append((byte)_bits);
                _bits >>= 8;
                _bitCount -= 8;
            }
        }

        public void Flush()
        {
            if (_bitCount > 0)
            {
                Append((byte)_bits);
                _bits = 0;
                _bitCount = 0;
            }

            if (_length > 0)
            {
                output.WriteByte((byte)_length);
                output.Write(_block, 0, _length);
                _length = 0;
            }
        }

        private void Append(byte value)
        {
            _block[_length++] = value;
            if (_length == _block.Length)
            {
                output.WriteByte((byte)_length);
                output.Write(_block);
                _length = 0;
            }
        }
    }
}
