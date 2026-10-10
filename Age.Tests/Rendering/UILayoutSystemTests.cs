using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins what the layout of the interface does with the anchors of an element at a resolution other than the one it was authored
/// against, which is the whole point of them: the same rectangle relative to the canvas, in pixels of any window.
/// </summary>
public sealed class UILayoutSystemTests
{
    private const float Tolerance = 0.01f;

    /// <summary>The resolutions a game is asked to look right at: its own, a screen of another size, a screen of another shape.</summary>
    public static TheoryData<float, float> Screens => new()
    {
        { 1280f, 720f },
        { 1920f, 1080f },
        { 3840f, 2160f },
        { 1484f, 911f },
        { 1024f, 768f },
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void UILayoutSystem_AnAnchoredCorner_KeepsItsMarginAtEveryResolution(float width, float height)
    {
        var world = new World();
        var renderer = new RecordingRenderer { ViewportSize = new Vector2(width, height) };
        var system = new UILayoutSystem(renderer);
        Entity canvas = CreateCanvas(world);
        Entity corner = CreateCorner(world);
        Entity literal = CreateLiteral(world);

        system.UpdateFrame(world, new GameTime(0d, 0d));

        CanvasComponent scaler = world.Get<CanvasComponent>(canvas);
        RectTransformComponent rect = world.Get<RectTransformComponent>(corner);
        float margin = 100f * scaler.Scale;

        rect.Size.Should().Be(new Vector2(200f, 50f) * scaler.Scale);
        rect.Position.X.Should().BeApproximately(width - margin - rect.Size.X, Tolerance);
        rect.Position.Y.Should().BeApproximately(height - margin - rect.Size.Y, Tolerance);

        // An element that a game placed by hand is left exactly as it was written.
        RectTransformComponent untouched = world.Get<RectTransformComponent>(literal);
        untouched.Position.Should().Be(new Vector2(10f, 20f));
        untouched.Size.Should().Be(new Vector2(30f, 40f));
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void UILayoutSystem_AStretchedElement_FillsTheWindowAtEveryResolution(float width, float height)
    {
        var world = new World();
        var renderer = new RecordingRenderer { ViewportSize = new Vector2(width, height) };
        var system = new UILayoutSystem(renderer);
        CreateCanvas(world);
        Entity shade = world.CreateEntity();
        world.Set(shade, new RectTransformComponent { Anchored = true, AnchorMax = new Vector2(1f, 1f), Visible = true });

        system.UpdateFrame(world, new GameTime(0d, 0d));

        RectTransformComponent rect = world.Get<RectTransformComponent>(shade);

        rect.Position.Should().Be(Vector2.Zero);
        rect.Size.X.Should().BeApproximately(width, 0.01f);
        rect.Size.Y.Should().BeApproximately(height, 0.01f);
    }

    [Fact]
    public void UILayoutSystem_AWorldWithoutACanvas_MeasuresTheAnchorsAgainstTheWindow()
    {
        var world = new World();
        var renderer = new RecordingRenderer { ViewportSize = new Vector2(1600f, 900f) };
        var system = new UILayoutSystem(renderer);
        Entity corner = CreateCorner(world);

        system.UpdateFrame(world, new GameTime(0d, 0d));

        RectTransformComponent rect = world.Get<RectTransformComponent>(corner);

        rect.Position.Should().Be(new Vector2(1600f - 300f, 900f - 150f));
        rect.Size.Should().Be(new Vector2(200f, 50f), "a world without a canvas is measured in pixels rather than in design units");
    }

    [Fact]
    public void UILayoutSystem_TheRootCanvasOfTheWorld_IsWhatTheAnchorsAreMeasuredAgainst()
    {
        var world = new World();
        var renderer = new RecordingRenderer { ViewportSize = new Vector2(1280f, 720f) };
        var system = new UILayoutSystem(renderer);
        Entity other = CreateCanvas(world, design: new Vector2(640f, 360f), isRoot: false);
        CreateCanvas(world, design: new Vector2(1280f, 720f), isRoot: true);
        Entity corner = CreateCorner(world);

        system.UpdateFrame(world, new GameTime(0d, 0d));

        // Every canvas resolved its own scaler, so a game that reads the one it authored sees the truth of it.
        world.Get<CanvasComponent>(other).Scale.Should().BeApproximately(2f, Tolerance);

        // Where the anchors are measured is the root canvas, which is the second one here.
        RectTransformComponent rect = world.Get<RectTransformComponent>(corner);
        rect.Position.Should().Be(new Vector2(1280f - 300f, 720f - 150f));
    }

    [Fact]
    public void UILayoutSystem_AScreenOfNothing_LeavesWhatTheGameWrote()
    {
        var world = new World();
        var renderer = new RecordingRenderer { ViewportSize = Vector2.Zero };
        var system = new UILayoutSystem(renderer);
        CreateCanvas(world);
        Entity corner = world.CreateEntity();
        world.Set(corner, new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(1f, 1f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(1f, 1f),
            AnchoredPosition = new Vector2(7f, 9f),
            Position = new Vector2(11f, 13f),
            SizeDelta = new Vector2(200f, 50f),
            Visible = true,
        });

        system.UpdateFrame(world, new GameTime(0d, 0d));

        // A renderer that is not attached to a window has no screen to lay anything out on, so what a game wrote stays as it is
        // rather than collapsing to the origin of a window that is not there.
        world.Get<RectTransformComponent>(corner).Position.Should().Be(new Vector2(11f, 13f));
        world.Get<RectTransformComponent>(corner).Size.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void UILayoutSystem_WithNoWorld_IsRefused()
    {
        var system = new UILayoutSystem(new RecordingRenderer());

        Action update = () => system.UpdateFrame(null!, new GameTime(0d, 0d));

        update.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UILayoutSystem_WithoutARenderer_IsRefused()
    {
        Action create = () => _ = new UILayoutSystem(null!);

        create.Should().Throw<ArgumentNullException>();
    }

    private static Entity CreateCanvas(World world, Vector2? design = null, bool isRoot = true)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new CanvasComponent
        {
            IsRoot = isRoot,
            DesignSize = design ?? new Vector2(1280f, 720f),
            ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
            Match = 0.5f,
        });

        return entity;
    }

    [Fact]
    public void UILayoutSystem_AChild_IsPlacedInsideTheRectangleOfItsParent()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new UILayoutSystem(renderer);
        CreateCanvas(world);

        // A panel anchored at the top-left corner with a size of its own, and a child stretched across its right half.
        Entity panel = world.CreateEntity();
        world.Set(panel, new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = Vector2.Zero,
            AnchorMax = Vector2.Zero,
            Pivot = Vector2.Zero,
            AnchoredPosition = new Vector2(100f, 50f),
            SizeDelta = new Vector2(400f, 200f),
            Visible = true,
        });

        Entity child = world.CreateEntity();
        world.Set(child, new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(0.5f, 0f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = Vector2.Zero,
            Visible = true,
        });
        world.Set(child, new ParentComponent { Parent = world.Reference(panel) });
        world.Set(panel, new ChildrenComponent { Children = [world.Reference(child)] });

        system.UpdateFrame(world, new GameTime(0d, 0d));

        // The panel is at (100, 50) with a size of 400x200, so its right half begins at 100 + 200 = 300 and is 200 wide and 200 tall.
        world.Get<RectTransformComponent>(child).Position.Should().Be(new Vector2(300f, 50f));
        world.Get<RectTransformComponent>(child).Size.Should().Be(new Vector2(200f, 200f));

        // Moving the panel moves the child with it, which is what the hierarchy is for.
        world.GetRef<RectTransformComponent>(panel).AnchoredPosition = new Vector2(200f, 50f);
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<RectTransformComponent>(child).Position.Should().Be(new Vector2(400f, 50f));
    }

    [Fact]
    public void UILayoutSystem_AChildOfAnElementThatIsNotThere_IsLaidOutAgainstTheCanvas()
    {
        var world = new World();
        var system = new UILayoutSystem(new RecordingRenderer());
        CreateCanvas(world);

        Entity orphan = world.CreateEntity();
        world.Set(orphan, new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(1f, 1f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(1f, 1f),
            AnchoredPosition = new Vector2(-100f, -100f),
            SizeDelta = new Vector2(200f, 50f),
            Visible = true,
        });

        // A reference that names an entity the world does not hold is a root rather than an element that stands nowhere.
        world.Set(orphan, new ParentComponent { Parent = new EntityRef(9999) });

        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<RectTransformComponent>(orphan).Position.Should().Be(new Vector2(1280f - 100f - 200f, 720f - 100f - 50f));
    }

    [Fact]
    public void UILayoutSystem_ATreeThatPointsBackAtItself_IsLaidOutOnce()
    {
        var world = new World();
        var system = new UILayoutSystem(new RecordingRenderer());
        CreateCanvas(world);

        Entity first = world.CreateEntity();
        Entity second = world.CreateEntity();
        world.Set(first, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(100f, 100f), Visible = true });
        world.Set(second, new RectTransformComponent { Anchored = true, SizeDelta = new Vector2(100f, 100f), Visible = true });
        world.Set(first, new ParentComponent { Parent = world.Reference(second) });
        world.Set(second, new ParentComponent { Parent = world.Reference(first) });
        world.Set(first, new ChildrenComponent { Children = [world.Reference(second)] });
        world.Set(second, new ChildrenComponent { Children = [world.Reference(first)] });

        Action update = () => system.UpdateFrame(world, new GameTime(0d, 0d));

        // A cycle is not a walk that never ends: the tree is visited once, and an element no root reached is laid out against the
        // canvas, so both elements end up placed rather than one looping forever.
        update.Should().NotThrow();
        world.Get<RectTransformComponent>(first).Size.Should().Be(new Vector2(100f, 100f));
        world.Get<RectTransformComponent>(second).Size.Should().Be(new Vector2(100f, 100f));
    }

    /// <summary>An element anchored to the bottom-right corner of the canvas with a margin of its own.</summary>
    private static Entity CreateCorner(World world)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new RectTransformComponent
        {
            Anchored = true,
            AnchorMin = new Vector2(1f, 1f),
            AnchorMax = new Vector2(1f, 1f),
            Pivot = new Vector2(1f, 1f),
            AnchoredPosition = new Vector2(-100f, -100f),
            SizeDelta = new Vector2(200f, 50f),
            Visible = true,
        });

        return entity;
    }

    /// <summary>An element whose rectangle a game wrote down itself, which the layout has no business touching.</summary>
    private static Entity CreateLiteral(World world)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new RectTransformComponent
        {
            Position = new Vector2(10f, 20f),
            Size = new Vector2(30f, 40f),
            Visible = true,
        });

        return entity;
    }

    /// <summary>A renderer that reports the size of a screen and draws nothing.</summary>
    private sealed class RecordingRenderer : IRenderer
    {
        public Vector2 ViewportSize { get; set; } = new(1280f, 720f);

        public void Attach(IWindowService window) { }

        public void SetCamera(Camera2D camera) { }

        public void BeginFrame(bool clear) { }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) { }

        public void DrawRectangle(Rect rect, Color color) { }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color) { }

        public void EndFrame() { }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(1);

        public void ReleaseTexture(TextureHandle texture) { }

        public void Dispose() { }
    }
}
