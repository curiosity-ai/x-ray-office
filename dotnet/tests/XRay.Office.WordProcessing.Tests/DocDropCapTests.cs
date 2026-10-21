using XRay.Office.TestKit;
using Shouldly;

namespace XRay.Office.WordProcessing.Tests;

/// <summary>
/// A Word drop cap and a positioned run in a <c>.doc</c>, measured against the 26.8 daily.
/// </summary>
/// <remarks>
/// <para>
/// A WW8 drop cap is a text frame whose own PAPX carries <c>sprmPDcs</c>; the importer builds no frame for
/// it and merges its letters into the next paragraph as that paragraph's drop
/// (<c>SwWW8ImplReader::StartApo</c>, <c>sw/source/filter/ww8/ww8par6.cxx</c>:2489-2494;
/// <c>ww8par.cxx</c>:3977-4049). Unlike writerfilter it leaves the letters' own formatting in place, and
/// a Word drop cap is lowered by <c>sprmCHpsPos</c>, so the letter's baseline is below the drop's.
/// </para>
/// <para>
/// Expectations are the reference's, from the fixtures' own render; see <c>tests/corpus/README.md</c>.
/// </para>
/// </remarks>
public sealed class DocDropCapTests
{
    /// <summary>The letter spans three lines, lowered by its stated position, and the text runs after it.</summary>
    [Fact]
    public void AWordDropCapSpansThreeLinesAndKeepsItsPosition()
    {
        List<DrawnWord> words = Words("drop-cap.doc");

        DrawnWord letter = words.Single(word => word.Size > 30);
        letter.Text.ShouldBe("W");
        letter.Size.ShouldBe(57.8, 0.2);
        letter.Baseline.ShouldBe(129.9, 0.15);
        words.First(word => word.Text == "AFTER").Left.ShouldBe(126.6, 0.2);
    }

    /// <summary>
    /// <c>sprmCHpsPos</c> moves a run by a percentage of its own size, as <c>Read_SubSuperProp</c>
    /// computes it (<c>ww8par6.cxx</c>:3551-3586).
    /// </summary>
    [Fact]
    public void AStatedPositionRaisesOrLowersTheRun()
    {
        List<DrawnWord> words = Words("character-position.doc");

        words.Single(word => word.Text == "Base").Baseline.ShouldBe(102.6, 0.1);
        words.Single(word => word.Text == "RAISED").Baseline.ShouldBe(99.15, 0.1);
        words.Single(word => word.Text == "LOWERED").Baseline.ShouldBe(107.1, 0.1);
    }

    private static List<DrawnWord> Words(string fileName)
    {
        string path = Corpus.Require(fileName);
        return ParagraphFrameTests.Draw(File.ReadAllBytes(path), Path.GetFileName(path));
    }
}
