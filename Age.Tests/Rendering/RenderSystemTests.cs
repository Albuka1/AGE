using Age.Assets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class RenderSystemTests
{
    [Fact]
    public void RenderSystem_SpriteOutsideTheCamera_IsNotDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(4000f, 4000f));

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().BeEmpty();
    }

    [Fact]
    public void RenderSystem_DrawsTheVisibleSpritesInAscendingZOrder()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(100f, 100f), zOrder: 2);
        CreateSprite(world, new Vector2(200f, 200f), zOrder: 0);
        CreateSprite(world, new Vector2(6000f, 6000f), zOrder: 1);

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().Equal(new Vector2(200f, 200f), new Vector2(100f, 100f));
    }

    [Fact]
    public void RenderSystem_SpriteThatOnlyPartlyOverlapsTheCamera_IsDrawn()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(-32f, -32f));

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().ContainSingle();
    }

    [Fact]
    public void RenderSystem_UsesTheBoxOfTheRotatedQuad()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());

        // A long sprite whose centre is just outside the right edge of the camera: upright it reaches into the frame.
        CreateSprite(world, new Vector2(1100f, 355f), size: new Vector2(400f, 10f));
        system.Render(world, Camera(1280f, 720f));
        renderer.Starts.Should().ContainSingle();

        // The same sprite, turned a quarter around its own centre, is a thin bar outside of the frame.
        var turnedWorld = new World();
        var turned = new RecordingRenderer();
        CreateSprite(turnedWorld, new Vector2(1100f, 355f), size: new Vector2(400f, 10f), rotation: MathF.PI / 2f);
        new RenderSystem(turned, new SpriteSorter()).Render(turnedWorld, Camera(1280f, 720f));

        turned.Starts.Should().BeEmpty();
    }

    [Fact]
    public void RenderSystem_CameraWithoutAViewportSize_DrawsEverything()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(4000f, 4000f));
        CreateSprite(world, new Vector2(-9000f, -9000f));

        system.Render(world, new Camera2D { Zoom = 1f });

        renderer.Starts.Should().HaveCount(2);
    }

    [Fact]
    public void RenderSystem_CameraWithALargerZoom_SeesMoreOfTheWorld()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var system = new RenderSystem(renderer, new SpriteSorter());
        CreateSprite(world, new Vector2(4000f, 100f));

        system.Render(world, Camera(1280f, 720f));
        renderer.Starts.Should().BeEmpty();

        system.Render(world, Camera(1280f, 720f, zoom: 4f));

        renderer.Starts.Should().ContainSingle();
    }

    [Fact]
    public void RenderSystem_SpriteWithAPath_DrawsTheImageOfThePathAndAsksForItOnce()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var images = new CountingImageLoader();
        var textures = new TextureService(images, renderer);
        var system = new RenderSystem(renderer, new SpriteSorter(), textures);

        CreateSprite(world, new Vector2(100f, 100f), path: "Textures/Tiles/tiles.bmp");
        system.Render(world, Camera(1280f, 720f));
        system.Render(world, Camera(1280f, 720f));

        images.Calls.Should().Be(1, "the handle of the first frame stays in the sprite");
        renderer.Textures.Should().HaveCount(2);
        renderer.Textures.Should().OnlyContain(texture => texture == renderer.Textures[0]);
        renderer.Textures[0].Id.Should().NotBe(0, "the image the path names was uploaded");
    }

    [Fact]
    public void RenderSystem_SpriteWhoseImageIsNotThere_DrawsThePlaceholderAndKeepsIt()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var textures = new TextureService(new CountingImageLoader { Fail = true }, renderer);
        Entity entity = CreateSprite(world, new Vector2(100f, 100f), path: "Textures/Nowhere/gone.png");

        new RenderSystem(renderer, new SpriteSorter(), textures).Render(world, Camera(1280f, 720f));

        renderer.Textures.Should().ContainSingle().Which.Should().Be(textures.Error, "a sprite whose image is missing is drawn as the placeholder");
        textures.MissingCount.Should().Be(1, "the same hole is reported once however many frames ask for the image of the path");
        world.Get<SpriteComponent>(entity).Texture.Should().Be(textures.Error, "and the placeholder is what the sprite carries, so a game can read what it draws");
    }

    [Fact]
    public void RenderSystem_SpriteWithoutAPath_IsDrawnAsASolidColorQuad()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var textures = new TextureService(new CountingImageLoader(), renderer);

        CreateSprite(world, new Vector2(100f, 100f));
        new RenderSystem(renderer, new SpriteSorter(), textures).Render(world, Camera(1280f, 720f));

        renderer.Textures.Should().ContainSingle().Which.Id.Should().Be(0, "a sprite that names no image is a solid color quad");
        textures.Count.Should().Be(0, "nothing was uploaded for it");
    }

    [Fact]
    public void RenderSystem_ASpriteWhoseTextureWasUnloaded_ResolvesItsPathAgain()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var images = new CountingImageLoader();
        var textures = new TextureService(images, renderer);
        var system = new RenderSystem(renderer, new SpriteSorter(), textures);
        CreateSprite(world, new Vector2(100f, 100f), path: "Textures/Tiles/tiles.bmp");

        system.Render(world, Camera(1280f, 720f));
        TextureHandle first = renderer.Textures[0];
        images.Calls.Should().Be(1);

        textures.UnloadAll();
        system.Render(world, Camera(1280f, 720f));

        images.Calls.Should().Be(2, "the handle of the first frame is gone, so the image of the path is loaded again");
        renderer.Textures.Should().HaveCount(2);
        renderer.Textures[1].Should().NotBe(first);
        renderer.Textures[1].Id.Should().NotBe(0, "the sprite is drawn from a texture that is alive rather than from the one that was released");
    }

    [Fact]
    public void RenderSystem_ASpriteWhosePathChanged_DrawsTheImageOfTheNewPath()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var textures = new TextureService(new CountingImageLoader(), renderer);
        var system = new RenderSystem(renderer, new SpriteSorter(), textures);
        Entity entity = CreateSprite(world, new Vector2(100f, 100f), path: "Textures/Tiles/one.bmp");

        system.Render(world, Camera(1280f, 720f));

        world.GetRef<SpriteComponent>(entity).TexturePath = "Textures/Tiles/two.bmp";
        system.Render(world, Camera(1280f, 720f));

        renderer.Textures.Should().HaveCount(2);
        renderer.Textures[1].Should().NotBe(renderer.Textures[0], "the path is what the image is, so a game swaps the image of a sprite by changing the path rather than by hunting for a handle");
        renderer.Textures[1].Id.Should().NotBe(0);
        textures.MissingCount.Should().Be(0);
    }

    [Fact]
    public void RenderSystem_ASpriteOfLayers_DrawsEveryLayerOfItAtTheBoxOfTheSprite()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var textures = new TextureService(new CountingImageLoader(), renderer);
        var system = new RenderSystem(renderer, new SpriteSorter(), textures);
        Entity entity = CreateSprite(world, new Vector2(100f, 100f), size: new Vector2(32f, 32f));
        world.GetRef<SpriteComponent>(entity).Layers =
        [
            new SpriteLayer { Name = "base", Image = "Textures/Tiles/one.bmp" },
            new SpriteLayer { Name = "over", Image = "Textures/Tiles/two.bmp" },
        ];

        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().Equal(new[] { new Vector2(100f, 100f), new Vector2(100f, 100f) }, "every layer of a sprite is drawn at the box of the sprite");
        renderer.Textures.Should().HaveCount(2);
        renderer.Textures[1].Should().NotBe(renderer.Textures[0], "a layer names an image of its own");
        renderer.Programs.Should().OnlyContain(shader => shader == null, "a layer that names no shader is drawn with the program of the engine");
        textures.MissingCount.Should().Be(0);
    }

    [Fact]
    public void RenderSystem_AShaderOfALayer_DrawsThatLayerWithItAndHandsTheEngineProgramBack()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var shaders = new RecordingShaderService();
        var system = new RenderSystem(renderer, new SpriteSorter(), shaders: shaders);
        Entity entity = CreateSprite(world, new Vector2(100f, 100f));
        world.GetRef<SpriteComponent>(entity).Layers =
        [
            new SpriteLayer { Name = "base" },
            new SpriteLayer { Name = "pulse", Shader = "Shaders/pulse.frag" },
            new SpriteLayer { Name = "over" },
        ];

        system.Render(world, Camera(1280f, 720f));

        shaders.Loaded.Should().Equal(new[] { "Shaders/pulse.frag" }, "the stage of a layer is compiled on the first frame that draws it");
        renderer.Programs.Should().HaveCount(3);
        renderer.Programs[0].Should().BeNull("the layer before the shader is drawn with the program of the engine");
        renderer.Programs[1].Should().NotBeNull("the layer that names a shader is drawn with it");
        shaders.IsAlive(renderer.Programs[1]!.Value).Should().BeTrue("the handle is one that the service still holds");
        renderer.Programs[2].Should().BeNull("the layer after the shader is drawn with the program of the engine again");
        renderer.ProgramAtEndOfFrame.Should().BeNull("the passes that follow the world draw with the program of the engine");

        system.Render(world, Camera(1280f, 720f));

        shaders.Loaded.Should().Equal(new[] { "Shaders/pulse.frag" }, "the pass keeps the program of a layer rather than asking for it on every frame");
    }

    [Fact]
    public void RenderSystem_AShaderThatCannotBeLoaded_IsAskedForOnceAndTheLayerIsDrawnWithoutIt()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var shaders = new RecordingShaderService { Fail = true };
        var system = new RenderSystem(renderer, new SpriteSorter(), shaders: shaders);
        Entity entity = CreateSprite(world, new Vector2(100f, 100f));
        world.GetRef<SpriteComponent>(entity).Layers = [new SpriteLayer { Name = "pulse", Shader = "Shaders/Nowhere/gone.frag" }];

        system.Render(world, Camera(1280f, 720f));
        system.Render(world, Camera(1280f, 720f));

        renderer.Starts.Should().HaveCount(2, "a layer whose shader cannot be loaded is drawn with the program of the engine rather than left out");
        renderer.Programs.Should().OnlyContain(shader => shader == null);
        shaders.Loaded.Should().Equal(new[] { "Shaders/Nowhere/gone.frag" }, "a stage that is not there is reported once rather than read on every frame");
    }

    [Fact]
    public void RenderSystem_AnotherAttachment_CompilesTheStagesOfTheLayersAgain()
    {
        var world = new World();
        var renderer = new RecordingRenderer();
        var shaders = new RecordingShaderService();
        var system = new RenderSystem(renderer, new SpriteSorter(), shaders: shaders);
        Entity entity = CreateSprite(world, new Vector2(100f, 100f));
        world.GetRef<SpriteComponent>(entity).Layers = [new SpriteLayer { Name = "pulse", Shader = "Shaders/pulse.frag" }];

        system.Render(world, Camera(1280f, 720f));
        renderer.AttachToAnotherWindow();
        system.Render(world, Camera(1280f, 720f));

        shaders.Loaded.Should().Equal(
            new[] { "Shaders/pulse.frag", "Shaders/pulse.frag" },
            "a program of the window before is gone with the device of it, so the stage is compiled again for the device that is there now");
    }

    private static Camera2D Camera(float width, float height, float zoom = 1f) => new()
    {
        Position = Vector2.Zero,
        Zoom = zoom,
        ViewportSize = new Vector2(width, height),
    };

    private static Entity CreateSprite(
        World world,
        Vector2 position,
        int zOrder = 0,
        Vector2? size = null,
        float rotation = 0f,
        string? path = null)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new TransformComponent { Position = position, Scale = new Vector2(1f, 1f), Rotation = rotation });
        world.Set(entity, new SpriteComponent
        {
            TexturePath = path,
            Size = size ?? new Vector2(64f, 64f),
            Color = Color.White,
            ZOrder = zOrder,
        });

        return entity;
    }

    /// <summary>An image loader that counts what it was asked for, and refuses everything when it is told to.</summary>
    private sealed class CountingImageLoader : IImageLoader
    {
        public int Calls { get; private set; }

        public bool Fail { get; init; }

        public ImageData Load(string relativePath)
        {
            Calls++;

            if (Fail)
            {
                throw new FileNotFoundException($"There is no image at '{relativePath}'.");
            }

            return new ImageData(2, 2, new byte[16]);
        }
    }

    /// <summary>A shader service that records what it was asked for, which is what tells a stage that is compiled once from one that is compiled on every frame.</summary>
    private sealed class RecordingShaderService : IShaderService
    {
        private readonly List<string> _loaded = [];
        private readonly Dictionary<string, ShaderHandle> _shaders = new(StringComparer.Ordinal);
        private readonly ResourcePool<string, uint> _slots = new();

        public List<string> Loaded => _loaded;

        public bool Fail { get; init; }

        public int Count => _shaders.Count;

        public ShaderHandle Load(string fragmentPath, string? vertexPath = null)
        {
            _loaded.Add(fragmentPath);

            if (Fail)
            {
                throw new FileNotFoundException($"There is no stage at '{fragmentPath}'.");
            }

            if (!_shaders.TryGetValue(fragmentPath, out ShaderHandle shader))
            {
                ResourceHandle slot = _slots.Add((uint)(_shaders.Count + 1), fragmentPath);
                shader = new ShaderHandle(slot, (uint)(_shaders.Count + 1), 0);
                _shaders[fragmentPath] = shader;
            }

            return shader;
        }

        public bool IsAlive(ShaderHandle shader) =>
            _shaders.ContainsValue(shader);

        public bool Unload(ShaderHandle shader)
        {
            foreach ((string path, ShaderHandle held) in _shaders)
            {
                if (held == shader)
                {
                    _shaders.Remove(path);
                    return true;
                }
            }

            return false;
        }

        public void UnloadAll() => _shaders.Clear();
    }

    private sealed class RecordingRenderer : IRenderer
    {
        public List<Vector2> Starts { get; } = [];

        public List<TextureHandle> Textures { get; } = [];

        /// <summary>Gets the shader that every drawn quad was drawn with, in the order the quads were drawn, where null is the program of the engine.</summary>
        public List<ShaderHandle?> Programs { get; } = [];

        /// <summary>Gets the shader that was used when the frame ended, which is what the passes after the world draw with.</summary>
        public ShaderHandle? ProgramAtEndOfFrame { get; private set; }

        public uint DeviceGeneration { get; private set; }

        private ShaderHandle? _currentShader;

        private int _created;

        public Vector2 ViewportSize => new(1280f, 720f);

        /// <summary>Makes the renderer report the attachment of another window, which is what a window swap does to a game.</summary>
        public void AttachToAnotherWindow() => DeviceGeneration++;

        public void Attach(IWindowService window)
        {
        }

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear)
        {
        }

        public void UseShader(ShaderHandle shader) => _currentShader = shader;

        public void ResetShader() => _currentShader = null;

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
            Starts.Add(position);
            Textures.Add(texture);
            Programs.Add(_currentShader);
        }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f) =>
            DrawSprite(texture, position, size, color, rotation);

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame() => ProgramAtEndOfFrame = _currentShader;

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(++_created);

        public void ReleaseTexture(TextureHandle texture)
        {
        }

        public void Dispose()
        {
        }
    }
}
