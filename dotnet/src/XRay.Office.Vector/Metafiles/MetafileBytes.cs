using System.Runtime.InteropServices;

namespace XRay.Office.Vector.Metafiles;

/// <summary>The array a metafile reader walks, without copying one it was handed whole.</summary>
/// <remarks>
/// The readers index a <c>byte[]</c> and never write to it, and the bytes they are given are nearly
/// always a picture part read into an array of its own. Copying that array first doubled the
/// allocation of every embedded metafile, which for one with a page-sized bitmap inside is megabytes.
/// </remarks>
internal static class MetafileBytes
{
    public static byte[] Whole(ReadOnlyMemory<byte> data)
        => MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment)
           && segment.Array is { } array
           && segment.Offset == 0
           && segment.Count == array.Length
            ? array
            : data.ToArray();
}
