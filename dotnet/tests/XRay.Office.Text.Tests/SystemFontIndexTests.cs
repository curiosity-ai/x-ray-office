using XRay.Office.Core.Graphics;
using XRay.Office.Text.Fonts;
using Shouldly;

namespace XRay.Office.Text.Tests;

/// <summary>
/// Tests that indexing the installed faces from a few tables answers what reading each face whole does.
/// </summary>
/// <remarks>
/// The index reads <c>head</c>, <c>OS/2</c>, <c>post</c> and <c>name</c> and nothing else, where it used
/// to read every file in full. Nothing about a face's family, weight, slant or pitch may move with
/// that, or every resolution in the tree moves with it — so this compares the two over every font
/// file on the machine, collections included.
/// </remarks>
public class SystemFontIndexTests
{
    [Fact]
    public void ThePartialReadAnswersWhatTheWholeFaceAnswers()
    {
        SystemFontIndex index = SystemFontIndex.Build();
        List<InstalledFace> faces = index.Faces.ToList();

        faces.Count.ShouldBeGreaterThan(0);

        foreach (InstalledFace installed in faces)
        {
            OpenTypeFace face = OpenTypeFace.ReadFile(installed.Path, installed.FaceIndex).ShouldNotBeNull();

            installed.FamilyName.ShouldBe(face.FamilyName, installed.FaceKey);
            installed.Weight.ShouldBe(face.Weight, installed.FaceKey);
            installed.IsItalic.ShouldBe(face.IsItalic, installed.FaceKey);
            installed.IsFixedPitch.ShouldBe(face.IsFixedPitch, installed.FaceKey);
        }
    }

    [Fact]
    public void ThePartialReadFindsEveryFaceOfACollection()
    {
        string? collection = SystemFontIndex.DefaultDirectories
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.ttc", SearchOption.AllDirectories))
            .FirstOrDefault();

        if (collection is null) Assert.Skip("No font collection is installed.");

        SfntFile whole = SfntFile.Open(File.ReadAllBytes(collection)).ShouldNotBeNull();
        SfntFile partial = SfntFile.OpenTables(collection, 0, "name").ShouldNotBeNull();

        partial.FaceCount.ShouldBe(whole.FaceCount);
        partial.Table("name").ToArray().ShouldBe(whole.Table("name").ToArray());
        partial.Has("glyf").ShouldBeFalse();
    }

    [Fact]
    public void TheSharedIndexIsBuiltOnce()
        => SystemFontIndex.Shared.ShouldBeSameAs(SystemFontIndex.Shared);

    [Fact]
    public void ResolversShareTheFacesTheyRead()
    {
        SystemFontResolver first = SystemFontResolver.Build();
        SystemFontResolver second = SystemFontResolver.Build();

        FontReference reference = first.Resolve(new FontRequest("Liberation Serif"));
        if (reference.FaceKey.Length == 0) Assert.Skip("No face resolves.");

        OpenTypeFace a = first.LoadOpenType(reference);
        OpenTypeFace b = second.LoadOpenType(reference);

        b.ShouldBeSameAs(a);
        second.ReferenceFor(b).ShouldNotBeNull().FaceKey.ShouldBe(reference.FaceKey);
    }

    [Fact]
    public void ASharedFaceOutlivesACollectionBetweenDocuments()
    {
        InstalledFace? installed = SystemFontIndex.Shared.Faces.FirstOrDefault();
        if (installed is null) Assert.Skip("No face is installed.");

        WeakReference<OpenTypeFace> first = ReadAndForget(installed.Value);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        first.TryGetTarget(out OpenTypeFace? kept).ShouldBeTrue();
        SharedFaces.Read(installed.Value.Path, installed.Value.FaceIndex).ShouldBeSameAs(kept);
    }

    private static WeakReference<OpenTypeFace> ReadAndForget(InstalledFace installed)
        => new(SharedFaces.Read(installed.Path, installed.FaceIndex).ShouldNotBeNull());
}
