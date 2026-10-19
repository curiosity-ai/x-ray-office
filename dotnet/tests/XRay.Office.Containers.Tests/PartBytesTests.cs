using XRay.Office.Containers;
using Shouldly;

namespace XRay.Office.Containers.Tests;

/// <summary>
/// Tests that reading a part into an array of its declared length gets the bytes right whatever the
/// declaration says, since the declaration comes from the file.
/// </summary>
public class PartBytesTests
{
    private static readonly byte[] Content = Enumerable.Range(0, 10_000).Select(i => (byte)(i * 7)).ToArray();

    /// <summary>A stream that cannot seek, as a zip entry's is.</summary>
    private sealed class Forward(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(0)]
    [InlineData(4_096)]
    [InlineData(20_000)]
    [InlineData(-1)]
    public void TheBytesAreTheStreamsWhateverLengthWasDeclared(long declared)
    {
        using Forward stream = new(Content);

        PartBytes.ReadAll(stream, declared).ShouldBe(Content);
    }

    [Fact]
    public void AnAbsurdDeclaredLengthIsNotAllocated()
    {
        using Forward stream = new(Content);

        PartBytes.ReadAll(stream, long.MaxValue).ShouldBe(Content);
    }
}
