using System.Xml.Linq;

namespace XRay.Office.Ooxml.DrawingML;

/// <summary>
/// The automatic numbering state of one DrawingML text body: which number each paragraph's
/// <c>a:buAutoNum</c> draws.
/// </summary>
/// <remarks>
/// <para>
/// Two mechanisms decide it and both are modelled, because neither alone reproduces the
/// reference.
/// </para>
/// <para>
/// <strong>The importer decides where a list restarts.</strong> This is
/// <c>ListNumberingState::markParagraph</c> as tdf#173712 (<c>8f242675c1b1</c>) rewrote it,
/// which is on the <c>libreoffice-26-8</c> branch after 26.8.1.1 and so ships in 26.8.2
/// (<c>oox/source/drawingml/textparagraphpropertiescontext.cxx</c>). Every paragraph is marked
/// once, with the bullet it states itself or, failing that, the one the body's own
/// <c>a:lstStyle</c> gives its level — never the layout's or the master's. A numbered one
/// restarts when its level holds no list yet or when its numbering type or <c>startAt</c>
/// differs from the last numbered paragraph's there, a missing <c>startAt</c> counting as 1; it
/// ends the lists deeper than itself. Anything else ends the lists at its own level and deeper.
/// </para>
/// <para>
/// <strong>26.8.1.1 itself does something else, and it is not what this ports.</strong> Its
/// importer (<c>markListNumbered</c>, before that commit) restarts every paragraph whose own
/// <c>a:buAutoNum</c> states <c>startAt</c>, so a list PowerPoint wrote with the same
/// <c>startAt</c> on every item repeats its first number: on <c>n90255.pptx</c> 26.8.1.1 draws
/// <c>3 3 II II C C iv iv f f</c> where 26.8.2 draws <c>3 4 II III C D iv v f g</c>, which is
/// also what PowerPoint draws. The 26.8.2 rule is kept deliberately, and that document differs
/// from the 26.8.1.1 reference until the benchmark moves.
/// </para>
/// <para>
/// <strong>The outliner decides what the number is.</strong> <c>Outliner::ImplGetNumbering</c>
/// (<c>editeng/source/outliner/outliner.cxx:1661-1714</c>) walks back from a paragraph through
/// its level: a deeper paragraph is skipped, a shallower one stops the walk, and so does one
/// whose numbering is not the same (<c>isSameNumbering</c>: type, prefix and suffix, all of
/// which an <c>ST_TextAutonumberScheme</c> name carries) or whose start is smaller. A larger
/// start moves the base up to it, and a paragraph marked as a restart is the last one counted.
/// Walked forwards, that is "one more than the previous number at this level while the run
/// holds, and the paragraph's own start otherwise", which is what <see cref="Next"/> keeps.
/// </para>
/// <para>
/// An empty paragraph consumes no number: the blank line an author leaves between two items is
/// still an <c>a:p</c>, inherits the level's bullet, and counting it makes the next item jump
/// from 2 to 4.
/// </para>
/// </remarks>
public sealed class DrawingListNumbering
{
    private const int LevelCount = 9;

    /// <summary>The importer's view: the last numbering stated at each level, null outside a list.</summary>
    private readonly Numbering?[] _stated = new Numbering?[LevelCount];

    /// <summary>The outliner's view: the numbering of the run each level is in, null when broken.</summary>
    private readonly Numbering?[] _run = new Numbering?[LevelCount];

    /// <summary>The last number drawn at each level.</summary>
    private readonly int[] _numbers = new int[LevelCount];

    private readonly record struct Numbering(string Type, int Start);

    /// <summary>
    /// Records one paragraph, in document order, and answers the number it draws.
    /// </summary>
    /// <remarks>
    /// Called for every paragraph of the body, numbered or not, because a paragraph that draws
    /// no number still ends lists.
    /// </remarks>
    /// <param name="level">The paragraph's zero-based outline level.</param>
    /// <param name="ownBullet">
    /// The bullet element (<c>a:buNone</c>, <c>a:buChar</c>, <c>a:buAutoNum</c> or
    /// <c>a:buBlip</c>) the paragraph's own <c>a:pPr</c> states, else the one its body's own
    /// <c>a:lstStyle</c> states for its level, else null. See <see cref="OwnBullet"/>.
    /// </param>
    /// <param name="autoNumber">
    /// The <c>a:buAutoNum</c> the whole chain resolves to, or null when the paragraph's bullet is
    /// anything else.
    /// </param>
    /// <param name="hasText">Whether the paragraph holds any text.</param>
    /// <returns>The formatted number, or null when the paragraph draws none.</returns>
    public string? Next(int level, XElement? ownBullet, XElement? autoNumber, bool hasText)
    {
        level = Math.Clamp(level, 0, LevelCount - 1);

        // The importer: ListNumberingState::markParagraph.
        bool restart = false;
        if (ownBullet is not null && ownBullet.Name.LocalName == "buAutoNum")
        {
            Numbering stated = Of(ownBullet);
            Array.Fill(_stated, null, level + 1, LevelCount - level - 1);
            restart = _stated[level] != stated;
            _stated[level] = stated;
        }
        else
        {
            Array.Fill(_stated, null, level, LevelCount - level);
        }

        // The outliner: a paragraph at this level stops every walk from a deeper one.
        Array.Fill(_run, null, level + 1, LevelCount - level - 1);

        if (autoNumber is null)
        {
            _run[level] = null;
            return null;
        }

        Numbering numbering = Of(autoNumber);
        bool continues = !restart
                         && _run[level] is { } previous
                         && previous.Type == numbering.Type
                         && previous.Start >= numbering.Start;

        if (!hasText)
        {
            // Transparent when it continues the run; otherwise it is where the next item's walk
            // stops, without a number of its own.
            if (!continues)
            {
                _run[level] = numbering;
                _numbers[level] = numbering.Start - 1;
            }

            return null;
        }

        _numbers[level] = continues ? _numbers[level] + 1 : numbering.Start;
        _run[level] = numbering;

        return DrawingTextBody.FormatNumber(numbering.Type, _numbers[level]);
    }

    /// <summary>
    /// The bullet a paragraph states for itself, for <see cref="Next"/>: the first of its own
    /// <c>a:pPr</c> and its body's <c>a:lstStyle</c> level that names a bullet kind.
    /// </summary>
    /// <param name="sources">The paragraph's own properties and its body's level style, in that order.</param>
    public static XElement? OwnBullet(IEnumerable<XElement?> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        foreach (XElement? source in sources)
        {
            if (source is null) continue;

            foreach (XElement child in source.Elements())
            {
                if (child.Name.LocalName is "buNone" or "buChar" or "buAutoNum" or "buBlip")
                    return child;
            }
        }

        return null;
    }

    private static Numbering Of(XElement autoNumber)
        => new(Drawing.Attribute(autoNumber, "type") ?? string.Empty,
               Drawing.Number(autoNumber, "startAt") ?? 1);
}
