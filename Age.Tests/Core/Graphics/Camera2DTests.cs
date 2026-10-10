using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class Camera2DTests
{
    [Fact]
    public void Camera2D_ScreenToWorld_IsTheInverseOfWhatTheRendererDrawsAQuadAt()
    {
        // The conversion is checked against the matrix the renderer really composes rather than against the algebra of the two
        // methods, because a formula that is the inverse of itself and of nothing else would pass any round-trip test.
        Camera2D camera = new() { Position = new Vector2(120f, -40f), Zoom = 2.5f, ViewportSize = new Vector2(1280f, 720f) };
        var world = new Vector2(300f, 210f);

        // This is what SilkRenderer uploads as uProjection, and what the vertex stage multiplies a position by.
        Matrix4x4 projection = camera.GetViewMatrix() * Matrix4x4.CreateOrthographic(1280f, 720f);
        Vector2 drawn = Project(projection, world, 1280f, 720f);

        Vector2 measured = camera.WorldToScreen(world);
        Vector2 readBack = camera.ScreenToWorld(drawn);

        measured.X.Should().BeApproximately(drawn.X, 0.0001f, "a position is drawn where the conversion says it is");
        measured.Y.Should().BeApproximately(drawn.Y, 0.0001f);
        readBack.X.Should().BeApproximately(world.X, 0.0001f, "and the conversion reads it back");
        readBack.Y.Should().BeApproximately(world.Y, 0.0001f);
    }

    [Fact]
    public void Camera2D_ScreenToWorldOfTheOriginOfTheViewport_IsWhereTheCameraStands()
    {
        Camera2D camera = new() { Position = new Vector2(300f, 150f), Zoom = 3f, ViewportSize = new Vector2(1280f, 720f) };

        camera.ScreenToWorld(Vector2.Zero).Should().Be(camera.Position, "the projection places the position of the camera at the top-left corner of the viewport");
        camera.WorldToScreen(camera.Position).Should().Be(Vector2.Zero);
    }

    [Fact]
    public void Camera2D_TheConversion_DoesNotDependOnTheViewportSize()
    {
        // A camera that was authored against a design area reports the window as its viewport, but the conversion is the view of
        // the camera alone: a game that never set a viewport size still converts, which is the same rule VisibleWorld follows.
        var position = new Vector2(64f, 32f);
        Camera2D sized = new() { Position = new Vector2(10f, 20f), Zoom = 2f, ViewportSize = new Vector2(1280f, 720f) };
        Camera2D unsized = new() { Position = sized.Position, Zoom = sized.Zoom };

        sized.WorldToScreen(position).Should().Be(unsized.WorldToScreen(position));
    }

    [Fact]
    public void Camera2D_TheConversion_ZoomedInShowsLessOfTheWorldPerPixel()
    {
        Camera2D near = new() { Zoom = 1f };
        Camera2D far = new() { Zoom = 4f };
        var screen = new Vector2(100f, 100f);

        near.ScreenToWorld(screen).Should().Be(new Vector2(100f, 100f));
        far.ScreenToWorld(screen).Should().Be(new Vector2(400f, 400f), "a camera that covers four times as much of the world puts the same pixel four times further out");
    }

    [Fact]
    public void Camera2D_AConversionWithAZoomThatIsNotAValidOne_IsRefused()
    {
        Camera2D zero = new() { Zoom = 0f };
        Camera2D negative = new() { Zoom = -1f };

        FluentActions.Invoking(() => zero.ScreenToWorld(Vector2.Zero)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => negative.WorldToScreen(Vector2.Zero)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Camera2D_ThePointerOfAConsoleCommand_ReadsBackAsWhereItWasDrawn()
    {
        // This is the body of the `point` command of the sample, driven through the console: a game finds what stands where by
        // asking this conversion, so the round trip through the console is the part that can be checked without a window.
        var console = new ConsoleService();
        Camera2D camera = new() { Position = new Vector2(120f, -40f), Zoom = 2.5f, ViewportSize = new Vector2(1280f, 720f) };
        var screen = new Vector2(640f, 360f);

        console.Register("point", "Reports where the pointer is in the world.", _ =>
        {
            Vector2 world = camera.ScreenToWorld(screen);
            Vector2 back = camera.WorldToScreen(world);

            console.Write($"{world.X:0}x{world.Y:0} is drawn back at {back.X:0}x{back.Y:0}");
        });

        console.Execute("point").Should().BeTrue();
        console.Output.Should().ContainSingle().Which.Should().Be("1720x860 is drawn back at 640x360");
    }

    /// <summary>Transforms a world position the way the vertex stage of the renderer does, for a viewport of that size.</summary>
    private static Vector2 Project(Matrix4x4 projection, Vector2 world, float width, float height)
    {
        // The vertex stage multiplies the position as a row by the transposed projection, and the clip space it lands in is the
        // normalized one: x from -1 to 1 across the width, y from 1 to -1 down the height, which is what SilKRenderer composes.
        float x = (world.X * projection.M11) + (world.Y * projection.M21) + projection.M41;
        float y = (world.X * projection.M12) + (world.Y * projection.M22) + projection.M42;

        return new Vector2((x + 1f) * 0.5f * width, (1f - y) * 0.5f * height);
    }
}
