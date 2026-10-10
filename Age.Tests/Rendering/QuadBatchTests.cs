using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class QuadBatchTests
{
    private static readonly Rect Unit = new(Vector2.Zero, new Vector2(1f, 1f));

    [Fact]
    public void QuadBatch_Add_PacksTheCornersAndThePartOfTheTexture()
    {
        var batch = new QuadBatch(4);
        Vector2[] corners = [new(1f, 2f), new(11f, 2f), new(1f, 22f), new(11f, 22f)];
        var uv = new Rect(new Vector2(0.25f, 0.5f), new Vector2(0.125f, 0.25f));

        batch.Add(corners, uv, Color.White, 7u, 3u);

        batch.Count.Should().Be(1);
        batch.Program.Should().Be(7u);
        batch.Texture.Should().Be(3u);
        batch.Vertices.Length.Should().Be(4 * QuadBatch.FloatsPerVertex);

        // The first corner is the top-left of the quad and takes the position of the part that is shown, and the corners that
        // follow it add the width and the height of that part.
        batch.Vertices[0].Should().Be(1f);
        batch.Vertices[1].Should().Be(2f);
        batch.Vertices[2].Should().Be(0.25f);
        batch.Vertices[3].Should().Be(0.5f);
        batch.Vertices[QuadBatch.FloatsPerVertex + 2].Should().Be(0.375f, "the top-right corner is one width further along the texture");
        batch.Vertices[(2 * QuadBatch.FloatsPerVertex) + 3].Should().Be(0.75f, "the bottom-left corner is one height further down");
        batch.Vertices[4].Should().Be(1f, "white is one in every channel");
    }

    [Fact]
    public void QuadBatch_Add_TakesAColourOfItsOwn()
    {
        var batch = new QuadBatch(1);

        batch.Add([Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], Unit, new Color(255, 128, 0), 1u, 1u);

        batch.Vertices[4].Should().Be(1f);
        batch.Vertices[5].Should().BeApproximately(128f / 255f, 0.0001f);
        batch.Vertices[6].Should().Be(0f);
        batch.Vertices[7].Should().Be(1f, "a colour without an alpha of its own is opaque");
    }

    [Fact]
    public void QuadBatch_CanAdd_OnlyHoldsOneProgramAndOneTexture()
    {
        var batch = new QuadBatch(4);

        batch.CanAdd(7u, 3u).Should().BeTrue("an empty batch takes the program and the texture of its first quad");
        batch.Add([Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], Unit, Color.White, 7u, 3u);

        batch.CanAdd(7u, 3u).Should().BeTrue();
        batch.CanAdd(7u, 4u).Should().BeFalse("another texture is what ends a batch");
        batch.CanAdd(8u, 3u).Should().BeFalse("another program is what ends a batch");
        batch.CanAdd(0u, 3u).Should().BeFalse("a quad without a program is not drawn at all");
    }

    [Fact]
    public void QuadBatch_Add_BeyondTheCapacity_IsRefused()
    {
        var batch = new QuadBatch(2);
        Vector2[] corners = [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero];

        batch.Add(corners, Unit, Color.White, 1u, 1u);
        batch.Add(corners, Unit, Color.White, 1u, 1u);

        batch.Count.Should().Be(2);
        batch.CanAdd(1u, 1u).Should().BeFalse("a full batch is what a caller draws before it adds another quad");
        Action add = () => batch.Add(corners, Unit, Color.White, 1u, 1u);
        add.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void QuadBatch_Indices_AreTheTwoTrianglesOfEveryQuad()
    {
        var batch = new QuadBatch(2);
        Vector2[] corners = [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero];

        batch.AllIndices.Length.Should().Be(2 * QuadBatch.IndicesPerQuad);
        batch.AllIndices[..6].ToArray().Should().Equal(0u, 1u, 2u, 1u, 3u, 2u);
        batch.AllIndices[6..].ToArray().Should().Equal(4u, 5u, 6u, 5u, 7u, 6u);

        batch.Add(corners, Unit, Color.White, 1u, 1u);

        batch.Indices.Length.Should().Be(QuadBatch.IndicesPerQuad, "what is drawn are the indices of the quads that are held");

        batch.Clear();

        batch.Count.Should().Be(0);
        batch.Program.Should().Be(0u);
        batch.Texture.Should().Be(0u);
        batch.Vertices.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void QuadBatch_FewerCornersThanAQuad_IsRefused()
    {
        var batch = new QuadBatch(1);

        Action add = () => batch.Add([Vector2.Zero, Vector2.Zero, Vector2.Zero], Unit, Color.White, 1u, 1u);

        add.Should().Throw<ArgumentException>().WithParameterName("corners");
    }
}
