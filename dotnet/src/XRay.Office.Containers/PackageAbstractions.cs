using XRay.Office.Core.Formats;

namespace XRay.Office.Containers;

/// <summary>
/// Uniform read-only access to the container underneath a document, whatever its
/// physical form.
/// </summary>
/// <remarks>
/// <para>
/// The three container families expose fundamentally the same shape — named parts
/// holding bytes — so the format readers above this layer should not care which they
/// are sitting on. An OLE2 directory entry, an OPC part and an ODF package entry all
/// become an <see cref="IPackagePart"/>.
/// </para>
/// <para>
/// Part names are normalised to forward-slash-separated paths without a leading
/// slash, e.g. <c>word/document.xml</c> or <c>ObjectPool/_1234/WordDocument</c>.
/// Comparison is ordinal and case-sensitive: OPC part names are case-sensitive per
/// spec, and OLE2 stream names are too.
/// </para>
/// </remarks>
public interface IPackage : IDisposable
{
    /// <summary>Which physical container this is.</summary>
    ContainerKind Kind { get; }

    /// <summary>Every part in the container, in no guaranteed order.</summary>
    IEnumerable<IPackagePart> Parts { get; }

    /// <summary>Looks up a part by normalised name, returning null if absent.</summary>
    IPackagePart? GetPart(string name);
}

/// <summary>A single named stream of bytes inside an <see cref="IPackage"/>.</summary>
public interface IPackagePart
{
    /// <summary>The normalised part name.</summary>
    string Name { get; }

    /// <summary>
    /// The declared media type, where the container records one. OPC always does;
    /// ODF records it only for the package as a whole; OLE2 never does.
    /// </summary>
    string? MediaType { get; }

    /// <summary>The uncompressed length in bytes, or -1 when not known without reading.</summary>
    long Length { get; }

    /// <summary>
    /// Opens the part's contents, decompressed and decrypted. Callers dispose the
    /// returned stream; parts may be opened more than once.
    /// </summary>
    Stream Open();
}

/// <summary>Reads a part, or any stream, whole into one array of exactly its length.</summary>
/// <remarks>
/// <para>
/// <c>CopyTo</c> a <see cref="MemoryStream"/> and <c>ToArray</c> doubles the buffer as it grows and then
/// copies it once more, so a picture part of a few megabytes cost three or four times its size in
/// allocation before anything had looked at it. Where the length is known in advance the bytes are read
/// straight into an array of that size.
/// </para>
/// <para>
/// A declared length is a statement the file makes about itself, so it is trusted only so far: the
/// array is pre-sized up to <see cref="MaxPresizedBytes"/> and grown past that the ordinary way, and a
/// stream that turns out longer or shorter than declared is still read correctly.
/// </para>
/// </remarks>
public static class PartBytes
{
    /// <summary>The largest declared length allocated up front.</summary>
    public const int MaxPresizedBytes = 64 * 1024 * 1024;

    /// <summary>Reads the whole part.</summary>
    public static byte[] ReadAll(this IPackagePart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        using Stream content = part.Open();
        return ReadAll(content, part.Length);
    }

    /// <summary>Reads the rest of a stream, using <paramref name="expectedLength"/> when it is known.</summary>
    /// <param name="content">The stream to read.</param>
    /// <param name="expectedLength">The length the stream is expected to have, or -1 when not known.</param>
    public static byte[] ReadAll(Stream content, long expectedLength = -1)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (expectedLength < 0 && content.CanSeek) expectedLength = content.Length - content.Position;
        if (expectedLength is < 0 or > MaxPresizedBytes) return ReadGrowing(content, []);

        byte[] exact = new byte[expectedLength];
        int read = 0;
        while (read < exact.Length)
        {
            int got = content.Read(exact, read, exact.Length - read);
            if (got == 0) return exact.AsSpan(0, read).ToArray();
            read += got;
        }

        // The declared length was a floor rather than the length: carry on the ordinary way.
        int next = content.ReadByte();
        return next < 0 ? exact : ReadGrowing(content, [.. exact, (byte)next]);
    }

    private static byte[] ReadGrowing(Stream content, byte[] prefix)
    {
        using MemoryStream buffer = new();
        buffer.Write(prefix);
        content.CopyTo(buffer);
        return buffer.ToArray();
    }
}
