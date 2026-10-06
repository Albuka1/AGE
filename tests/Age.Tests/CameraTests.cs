using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class CameraTests
{
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

    private static Vector2 Project(Matrix4x4 matrix, Vector2 point) => new(
        (point.X * matrix.M11) + (point.Y * matrix.M21) + matrix.M41,
        (point.X * matrix.M12) + (point.Y * matrix.M22) + matrix.M42);
}
