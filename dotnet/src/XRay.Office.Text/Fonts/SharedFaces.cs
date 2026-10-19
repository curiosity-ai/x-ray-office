using System.Globalization;

namespace XRay.Office.Text.Fonts;

/// <summary>
/// Faces read from disk, shared by everything in the process that reads them by path.
/// </summary>
/// <remarks>
/// <para>
/// A resolver, a raster sink and a PDF catalogue are each built per document, so each used to read
/// and parse every face it touched for itself: the body face, the glyph-fallback candidates a search
/// tried (several whole files for one uncovered character, and a CJK collection can be tens of
/// megabytes), and again the same faces at drawing time to look for colour bitmaps. An
/// <see cref="OpenTypeFace"/> is immutable once read, so one instance serves them all.
/// </para>
/// <para>
/// The faces used most recently are held strongly up to a budget of file bytes,
/// <see cref="DefaultBudgetBytes"/> unless <see cref="BudgetVariable"/> says otherwise in megabytes,
/// so the next document finds them however the garbage collector has run in between. Past the budget
/// the least recently used are let go, and are still found while anything else holds them. A face
/// whose file has changed size or modification time since it was read is read again.
/// </para>
/// </remarks>
public static class SharedFaces
{
    /// <summary>The environment variable that sets the budget, in megabytes; <c>0</c> holds nothing.</summary>
    public const string BudgetVariable = "XRAY_OFFICE_FONT_CACHE_MB";

    /// <summary>The budget when the variable is absent: enough for a document's faces and its fallbacks.</summary>
    public const long DefaultBudgetBytes = 256L * 1024 * 1024;

    private static readonly long Budget = ReadBudget();

    private static readonly Lock Gate = new();

    private static readonly Dictionary<(string Path, int Index), Entry> Entries = [];

    private static readonly LinkedList<(string Path, int Index)> Recent = [];

    private static long _held;

    private sealed class Entry((string Path, int Index) key, long length, DateTime modified, OpenTypeFace face)
    {
        public (string Path, int Index) Key { get; } = key;

        public long Length { get; } = length;

        public DateTime Modified { get; } = modified;

        public WeakReference<OpenTypeFace> Weak { get; } = new(face);

        public OpenTypeFace? Strong { get; set; }

        public LinkedListNode<(string Path, int Index)>? Node { get; set; }
    }

    /// <summary>
    /// The face at <paramref name="index"/> of the file at <paramref name="path"/>, or null when the
    /// file is not a font this reader understands.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static OpenTypeFace? Read(string path, int index = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        FileInfo info = new(path);
        if (!info.Exists) throw new FileNotFoundException("The font file does not exist.", path);

        long length = info.Length;
        DateTime modified = info.LastWriteTimeUtc;
        (string, int) key = (path, index);

        lock (Gate)
        {
            if (Entries.TryGetValue(key, out Entry? entry)
                && entry.Length == length
                && entry.Modified == modified
                && entry.Weak.TryGetTarget(out OpenTypeFace? cached))
            {
                Hold(entry, cached);
                return cached;
            }
        }

        // Read outside the lock: a slow disk must not stall every other thread's lookups. Two threads
        // reading one face at once both read it, and the second to finish takes the first one's, so
        // the process still holds one instance per face.
        OpenTypeFace? face = OpenTypeFace.ReadFile(path, index);
        if (face is null) return null;

        lock (Gate)
        {
            if (Entries.TryGetValue(key, out Entry? raced)
                && raced.Length == length
                && raced.Modified == modified
                && raced.Weak.TryGetTarget(out OpenTypeFace? first))
            {
                Hold(raced, first);
                return first;
            }

            if (Entries.Remove(key, out Entry? stale)) Release(stale);

            Entry entry = new(key, length, modified, face);
            Entries[key] = entry;
            Hold(entry, face);
        }

        return face;
    }

    private static void Hold(Entry entry, OpenTypeFace face)
    {
        if (Budget <= 0) return;

        if (entry.Node is { } node)
        {
            Recent.Remove(node);
            Recent.AddFirst(node);
            return;
        }

        entry.Strong = face;
        entry.Node = Recent.AddFirst(entry.Key);
        _held += entry.Length;

        while (_held > Budget && Recent.Last is { } last && last != entry.Node)
        {
            Release(Entries[last.Value]);
        }
    }

    private static void Release(Entry entry)
    {
        if (entry.Node is not { } node) return;

        Recent.Remove(node);
        entry.Node = null;
        entry.Strong = null;
        _held -= entry.Length;
    }

    private static long ReadBudget()
    {
        string? value = Environment.GetEnvironmentVariable(BudgetVariable);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long megabytes)
               && megabytes >= 0
            ? megabytes * 1024 * 1024
            : DefaultBudgetBytes;
    }
}
