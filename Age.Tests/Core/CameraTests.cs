using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class CameraTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Camera2D_GetViewMatrix_PositionZeroZoomOne_IsIdentity()
    {
        var camera = new Camera2D
        {
            Position = Vector2.Zero,
            Zoom = 1f,
            ViewportSize = new Vector2(1280f, 720f),
        };

        Matrix4x4 matrix = camera.GetViewMatrix();

        matrix.Should().Be(Matrix4x4.Identity);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Camera2D_GetViewMatrix_MovesCameraPositionToViewOrigin(float zoom)
    {
        var camera = new Camera2D
        {
            Position = new Vector2(120f, 64f),
            Zoom = zoom,
            ViewportSize = new Vector2(1280f, 720f),
        };

        Vector2 origin = Project(camera.GetViewMatrix(), new Vector2(120f, 64f));

        origin.Should().Be(Vector2.Zero);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Camera2D_GetViewMatrix_MapsCameraPositionToClipSpaceOrigin(float zoom)
    {
        var camera = new Camera2D
        {
            Position = new Vector2(120f, 64f),
            Zoom = zoom,
            ViewportSize = new Vector2(1280f, 720f),
        };

        Matrix4x4 matrix = camera.GetViewMatrix() * Matrix4x4.CreateOrthographic(1280f, 720f);

        // The camera position is the world point at the viewport origin, the top-left corner of clip space.
        (float x, float y, float w) = ProjectClip(matrix, camera.Position);

        x.Should().BeApproximately(-1f, Tolerance);
        y.Should().BeApproximately(1f, Tolerance);

        // Vectors are applied as v * M, so the translation has to reach x and y and leave w alone.
        w.Should().BeApproximately(1f, Tolerance);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Camera2D_GetViewMatrix_ZoomNotGreaterThanZero_ThrowsInvalidOperationException(float zoom)
    {
        var camera = new Camera2D
        {
            Position = Vector2.Zero,
            Zoom = zoom,
            ViewportSize = new Vector2(1280f, 720f),
        };

        Action act = () => camera.GetViewMatrix();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Camera2D_Fit_AWindowOfTheShapeOfTheDesign_ShowsExactlyThatArea()
    {
        Camera2D camera = Camera2D.Fit(new Vector2(1280f, 720f), new Vector2(1600f, 900f));

        // The world is scaled by one over the zoom, so the zoom of a design that fills the window is the ratio of the two.
        camera.Zoom.Should().BeApproximately(0.8f, Tolerance);
        camera.ViewportSize.Should().Be(new Vector2(1600f, 900f));
        camera.VisibleWorld.Size.X.Should().BeApproximately(1280f, 0.01f);
        camera.VisibleWorld.Size.Y.Should().BeApproximately(720f, 0.01f);
    }

    [Fact]
    public void Camera2D_Fit_AContainedDesignInASquarerWindow_ShowsMoreOfItThanItCrops()
    {
        // A window in 4:3 for a design in 16:9: the zoom that contains the design is the one of its width, so the view is
        // taller than the design by the difference of the shapes, and nothing of the design is cropped.
        Camera2D camera = Camera2D.Fit(new Vector2(1280f, 720f), new Vector2(1600f, 1200f), CameraFit.Contain);

        camera.Zoom.Should().BeApproximately(0.8f, Tolerance);
        camera.VisibleWorld.Size.X.Should().BeApproximately(1280f, 0.01f);
        camera.VisibleWorld.Size.Y.Should().BeApproximately(960f, 0.01f);
    }

    [Fact]
    public void Camera2D_Fit_ACoveringDesignInASquarerWindow_KeepsTheDesignHeightAndCropsTheWidth()
    {
        Camera2D camera = Camera2D.Fit(new Vector2(1280f, 720f), new Vector2(1600f, 1200f), CameraFit.Cover);

        camera.Zoom.Should().BeApproximately(0.6f, Tolerance);
        camera.VisibleWorld.Size.X.Should().BeApproximately(960f, 0.01f);
        camera.VisibleWorld.Size.Y.Should().BeApproximately(720f, 0.01f);
    }

    [Theory]
    [InlineData(1280f, 720f)]
    [InlineData(3840f, 2160f)]
    public void Camera2D_Fit_ADesignFillingTheWindow_IsTheSameScaleOnBothAxes(float width, float height)
    {
        Camera2D camera = Camera2D.Fit(new Vector2(1280f, 720f), new Vector2(width, height), CameraFit.Cover);

        camera.Zoom.Should().BeApproximately(1280f / width, Tolerance);
        camera.Zoom.Should().BeApproximately(720f / height, Tolerance);
    }

    [Fact]
    public void Camera2D_Fit_ADesignThatIsNotARealArea_IsRefused()
    {
        Action fit = () => Camera2D.Fit(Vector2.Zero, new Vector2(1280f, 720f));

        fit.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Camera2D_Fit_AWindowThatIsNotARealSize_IsRefused()
    {
        Action fit = () => Camera2D.Fit(new Vector2(1280f, 720f), Vector2.Zero);

        fit.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static Vector2 Project(Matrix4x4 matrix, Vector2 point)
    {
        (float x, float y, _) = ProjectClip(matrix, point);
        return new Vector2(x, y);
    }

    private static (float X, float Y, float W) ProjectClip(Matrix4x4 matrix, Vector2 point) => (
        (point.X * matrix.M11) + (point.Y * matrix.M21) + matrix.M41,
        (point.X * matrix.M12) + (point.Y * matrix.M22) + matrix.M42,
        (point.X * matrix.M14) + (point.Y * matrix.M24) + matrix.M44);
}
