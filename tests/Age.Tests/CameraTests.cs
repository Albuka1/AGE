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
