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
}
