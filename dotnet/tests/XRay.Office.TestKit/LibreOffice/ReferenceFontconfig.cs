using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace XRay.Office.TestKit.LibreOffice;

/// <summary>
/// The fontconfig configuration the reference <c>soffice</c> is run under: the machine's own,
/// less the two additions that make it resolve faces this tree cannot see.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why the reference needs one at all.</strong> This container's <c>/etc/fonts</c> carries
/// two things a stock fontconfig does not, and both reach LibreOffice and not this tree, because
/// this tree reads font <em>preferences</em> out of the configuration but discovers the font
/// files itself (<c>FontconfigPreferences</c>, <c>SystemFontIndex</c>):
/// </para>
/// <list type="number">
///   <item><description>
///     <b>A local preference file.</b> <c>conf.d/56-prefer-inter.conf</c>, a regular file rather
///     than a packaged symlink, prefers Inter for <c>sans-serif</c>, <c>Helvetica Neue</c> and the
///     UI families. Under it the reference draws an unknown family, <c>Aptos</c> and
///     <c>Helvetica Neue</c> in Inter, which this tree never does, and the harness's own fallback
///     probe in <see cref="LibreOfficeReference"/> refuses the reference — which is why the whole
///     fidelity suite skipped against every installed LibreOffice until this existed.
///   </description></item>
///   <item><description>
///     <b><c>fonts-urw-base35</c>.</b> Its configuration aliases the PostScript families and their
///     Microsoft look-alikes to URW's clones, so the reference draws <c>Bookman Old Style</c> in
///     URW Bookman, <c>Century Schoolbook</c> in C059, <c>Palatino Linotype</c> in P052,
///     <c>Times</c> in Nimbus Roman, <c>Courier</c> in Nimbus Mono PS, <c>Arial Narrow</c> in Nimbus
///     Sans Narrow and so on, where this tree draws DejaVu or Liberation.
///   </description></item>
/// </list>
/// <para>
/// Measured on one-paragraph probes, one family each, read back with <c>pdffonts</c>, against
/// <c>LibreOfficeDev 26.8.3.0.0</c> on 2026-10-21: of 23 families, the reference agrees with this
/// tree on <b>4</b> under the machine's configuration and on <b>17</b> under this one, and this tree
/// draws the same face for all 23 under both. The six that still differ are this tree's side: it finds URW's Nimbus Sans for <c>Century Gothic</c>,
/// <c>Verdana</c> and <c>Tahoma</c> because it scans the font directories itself, it draws
/// <c>Bookman Old Style</c> and <c>Century Schoolbook</c> in DejaVu Serif where the reference
/// falls back to DejaVu Sans, and it does not draw <c>Symbol</c> in OpenSymbol. A machine
/// without <c>fonts-urw-base35</c> closes the first three; that is a property of the machine,
/// not something to model.
/// </para>
/// <para>
/// <strong>Only the reference is given it.</strong> This tree's side runs in the test process
/// under whatever the process was started with. It does honour <c>FONTCONFIG_FILE</c>, for the
/// preferences it reads (<c>FontconfigPreferences</c>), but it resolved the same face for all 23
/// probe families under both configurations, so handing it this one would change nothing there.
/// </para>
/// <para>
/// <strong>Generated, not checked in</strong>, because it is the machine's configuration with two
/// things taken out, and the machine's configuration is not this repository's to copy: the
/// result is the system <c>fonts.conf</c> with its <c>conf.d</c> include replaced by one include
/// per kept file, in the order fontconfig would read them, plus a <c>rejectfont</c> for every URW
/// base-35 file. A caller that has set <c>FONTCONFIG_FILE</c> itself has chosen, and is left
/// alone — then both sides read the same file.
/// </para>
/// </remarks>
public static class ReferenceFontconfig
{
    /// <summary>The machine's root configuration, which is what is copied.</summary>
    private const string SystemRoot = "/etc/fonts/fonts.conf";

    /// <summary>The include the root configuration reads its drop-in directory through.</summary>
    private const string DropInInclude = "<include ignore_missing=\"yes\">conf.d</include>";

    /// <summary>The directories fontconfig's stock configuration scans for font files.</summary>
    private static readonly string[] FontRoots = ["/usr/share/fonts", "/usr/local/share/fonts"];

    /// <summary>
    /// The configuration file to hand <c>soffice</c> as <c>FONTCONFIG_FILE</c>, or null to leave its
    /// environment alone.
    /// </summary>
    /// <remarks>
    /// Null when the caller already set <c>FONTCONFIG_FILE</c>, when the machine has no
    /// <c>/etc/fonts/fonts.conf</c> to start from, or when that file does not read its drop-ins
    /// the stock way and so cannot be edited safely.
    /// </remarks>
    public static string? File => Generated.Value;

    private static readonly Lazy<string?> Generated = new(Generate);

    /// <summary>
    /// The configuration text for a root file and a set of drop-ins, or null when the root does
    /// not include its drop-ins the stock way.
    /// </summary>
    /// <param name="root">The text of the system <c>fonts.conf</c>.</param>
    /// <param name="dropIns">The drop-in files to keep, in the order to read them.</param>
    /// <param name="rejected">The font files the reference must not see.</param>
    public static string? Compose(
        string root, IEnumerable<string> dropIns, IEnumerable<string> rejected)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(dropIns);
        ArgumentNullException.ThrowIfNull(rejected);

        int at = root.IndexOf(DropInInclude, StringComparison.Ordinal);
        if (at < 0) return null;

        StringBuilder includes = new();
        foreach (string file in dropIns)
        {
            includes.Append(CultureInfo.InvariantCulture, $"<include ignore_missing=\"yes\">{Escape(file)}</include>\n\t");
        }

        StringBuilder rejections = new();
        List<string> globs = [.. rejected];
        if (globs.Count > 0)
        {
            rejections.Append("\t<selectfont><rejectfont>\n");
            foreach (string glob in globs)
                rejections.Append(CultureInfo.InvariantCulture, $"\t\t<glob>{Escape(glob)}</glob>\n");
            rejections.Append("\t</rejectfont></selectfont>\n");
        }

        string text = root[..at] + includes.ToString().TrimEnd() + root[(at + DropInInclude.Length)..];

        int close = text.LastIndexOf("</fontconfig>", StringComparison.Ordinal);
        if (close < 0) return null;

        return text[..close] + rejections + text[close..];
    }

    /// <summary>
    /// Whether a drop-in is a local family preference, which is what is left out.
    /// </summary>
    /// <remarks>
    /// A packaged drop-in is a symbolic link into a <c>conf.avail</c> directory; a local one is a
    /// regular file. Only a local file that edits families is dropped — one that only sets
    /// rendering options, such as this container's <c>12-unhinted-grayscale.conf</c>, changes no
    /// face and stays, so the reference is otherwise configured exactly as the machine is.
    /// </remarks>
    /// <param name="path">The drop-in's path.</param>
    /// <param name="isLink">Whether it is a symbolic link.</param>
    /// <param name="text">Its contents.</param>
    public static bool IsLocalFamilyPreference(string path, bool isLink, string text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);

        if (isLink) return false;

        return text.Contains("<alias", StringComparison.Ordinal)
               || text.Contains("name=\"family\"", StringComparison.Ordinal);
    }

    /// <summary>Whether a font file belongs to <c>fonts-urw-base35</c>.</summary>
    /// <remarks>
    /// By the directory the package installs into, or by a file name that package installs there:
    /// it also puts real copies of 35 of its Type 1 faces under <c>X11/Type1</c>, beside other
    /// packages' files, and fontconfig scans those under their own names.
    /// </remarks>
    /// <param name="path">The path fontconfig sees.</param>
    /// <param name="urwStems">The file names, without extension, found under a <c>urw-base35</c> directory.</param>
    public static bool IsUrwBase35(string path, IReadOnlySet<string> urwStems)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(urwStems);

        return path.Contains("/urw-base35/", StringComparison.Ordinal)
               || urwStems.Contains(System.IO.Path.GetFileNameWithoutExtension(path));
    }

    private static string? Generate()
    {
        if (Environment.GetEnvironmentVariable("FONTCONFIG_FILE") is { Length: > 0 }) return null;
        if (!System.IO.File.Exists(SystemRoot)) return null;

        try
        {
            string directory = System.IO.Path.GetDirectoryName(SystemRoot)!;
            string dropInDirectory = System.IO.Path.Combine(directory, "conf.d");

            List<string> dropIns = [];
            if (Directory.Exists(dropInDirectory))
            {
                string[] files = Directory.GetFiles(dropInDirectory, "*.conf");

                // fontconfig reads a directory's files in byte order of their names.
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string file in files)
                {
                    FileInfo info = new(file);
                    bool isLink = info.LinkTarget is not null;
                    if (IsLocalFamilyPreference(file, isLink, System.IO.File.ReadAllText(file))) continue;
                    dropIns.Add(file);
                }
            }

            List<string> fonts = [];
            foreach (string root in FontRoots)
            {
                if (Directory.Exists(root))
                    fonts.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
            }

            HashSet<string> urwStems = new(StringComparer.Ordinal);
            foreach (string file in fonts)
            {
                if (file.Contains("/urw-base35/", StringComparison.Ordinal))
                    urwStems.Add(System.IO.Path.GetFileNameWithoutExtension(file));
            }

            List<string> rejected = [.. fonts.Where(file => IsUrwBase35(file, urwStems))];
            rejected.Sort(StringComparer.Ordinal);

            string? text = Compose(System.IO.File.ReadAllText(SystemRoot), dropIns, rejected);
            if (text is null) return null;

            // Keyed on the content, so concurrent test processes on one machine share one file and
            // a change to the machine's configuration gets a new one rather than a stale reuse.
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12]
                .ToLowerInvariant();
            string output = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "xray-office-fontconfig-" + hash, "fonts.conf");

            if (!System.IO.File.Exists(output))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
                string temporary = output + "." + Guid.NewGuid().ToString("N")[..8];
                System.IO.File.WriteAllText(temporary, text);
                System.IO.File.Move(temporary, output, overwrite: true);
            }

            return output;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Escape(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal)
               .Replace("<", "&lt;", StringComparison.Ordinal)
               .Replace(">", "&gt;", StringComparison.Ordinal);
}
