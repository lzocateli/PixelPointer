using System.Formats.Tar;
using System.IO.Compression;
using PixelPointer.Core.Updates;

namespace PixelPointer.App.Updates;

/// <summary>
/// Extrai somente os arquivos esperados do pacote, no nível raiz e como arquivos regulares. Entradas extras são
/// ignoradas; links, diretórios ou duplicatas com nome esperado e caminhos fora do destino são recusados.
/// </summary>
internal static class PackageExtractor
{
    internal static void Extract(string archive, PackageLayout layout, string destination)
    {
        Directory.CreateDirectory(destination);
        var pending = new HashSet<string>(layout.Files, StringComparer.Ordinal);
        long total = 0;
        if (layout.IsZip)
        {
            using var zip = ZipFile.OpenRead(archive);
            foreach (var entry in zip.Entries)
            {
                var name = Normalize(entry.FullName);
                if (!layout.Files.Contains(name))
                {
                    continue;
                }

                if (!pending.Remove(name))
                {
                    throw new InvalidDataException($"O pacote contém {name} mais de uma vez.");
                }

                using var source = entry.Open();
                total = Copy(source, Path.Combine(destination, name), total, entry.Length);
            }
        }
        else
        {
            using var file = File.OpenRead(archive);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);
            while (tar.GetNextEntry() is { } entry)
            {
                var name = Normalize(entry.Name);
                if (!layout.Files.Contains(name))
                {
                    continue;
                }

                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null)
                {
                    throw new InvalidDataException($"{name} no pacote não é um arquivo regular.");
                }

                if (!pending.Remove(name))
                {
                    throw new InvalidDataException($"O pacote contém {name} mais de uma vez.");
                }

                total = Copy(entry.DataStream, Path.Combine(destination, name), total, entry.Length);
            }
        }

        if (pending.Count > 0)
        {
            throw new InvalidDataException($"O pacote não contém {string.Join(", ", pending.Order(StringComparer.Ordinal))}.");
        }
    }

    private static string Normalize(string name) => name.StartsWith("./", StringComparison.Ordinal) ? name[2..] : name;

    private static long Copy(Stream source, string path, long total, long declared)
    {
        if (declared < 0 || total + declared > PackageLayout.MaxPackageBytes * 4)
        {
            throw new InvalidDataException("O conteúdo do pacote excede o limite aceito.");
        }

        using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = source.Read(buffer)) > 0)
        {
            written += read;
            if (written > declared)
            {
                throw new InvalidDataException("Entrada do pacote maior que o tamanho declarado.");
            }

            target.Write(buffer, 0, read);
        }

        return written == declared ? total + written : throw new InvalidDataException("Entrada do pacote truncada.");
    }
}
