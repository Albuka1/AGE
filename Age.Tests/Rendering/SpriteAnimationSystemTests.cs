using Age.Assets;
using Age.Core;
using Age.Rendering;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class SpriteAnimationSystemTests : IDisposable
{
    private const string Sheet =
        "version: 1\n"
        + "image: Textures/Entities/goblin.bmp\n"
        + "cell:\n"
        + "  X: 16\n"
        + "  Y: 16\n"
        + "columns: 4\n"
        + "rows: 3\n"
        + "states:\n"
        + "  walk:\n"
        + "    row: 0\n"
        + "    frames: 4\n"
        + "    delay: 0.1\n"
        + "  attack:\n"
        + "    row: 1\n"
        + "    frames: 3\n"
        + "    delay: 0.1\n"
        + "    loop: false\n"
        + "  idle:\n"
        + "    row: 2\n"
        + "    frames: 1\n"
        + "    delay: 0.4\n"
        + "  flurry:\n"
        + "    row: 1\n"
        + "    frames: 2\n"
        + "    delays:\n"
        + "      - 0.05\n"
        + "      - 0.2\n"
        + "    loop: false\n";

    private readonly string _root;
    private readonly World _world = new();
    private readonly List<SpriteAnimationFinishedEvent> _finished = [];
    private readonly SpriteSheetService _sheets;
    private readonly TextureService _textures;
    private readonly SpriteAnimationSystem _system;

    public SpriteAnimationSystemTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "age-animation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "goblin.yml"), Sheet);

        var assets = new NullAssetLoader();
        assets.Initialize(_root);

        _textures = new TextureService(new FakeImageLoader(), new FakeRenderer());
        _sheets = new SpriteSheetService(assets, _textures);
        _system = new SpriteAnimationSystem(_sheets);
        _world.Events.Subscribe<SpriteAnimationFinishedEvent>((_, @event) => _finished.Add(@event));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void SpriteAnimationSystem_AStateThatDoesNotLoop_FinishesOnItsLastFrame()
    {
        Entity entity = Animate("attack");

        Step(0.1f);
        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(2);
        _finished.Should().BeEmpty("the state still has its last frame to show");

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(2, "the last frame of a state that does not loop stays on screen");
        _world.Get<SpriteAnimationComponent>(entity).Paused.Should().BeTrue("the play stands still until a game says what comes next");
        _finished.Should().ContainSingle("three frames of a tenth of a second are over after three steps");
        _finished[0].State.Should().Be("attack");
        _finished[0].Entity.Should().Be(entity);
    }

    [Fact]
    public void SpriteAnimationSystem_AStateThatStandsStill_DoesNotReportAgain()
    {
        Entity entity = Animate("attack");
        Step(0.1f);
        Step(0.1f);
        Step(0.1f);
        _finished.Should().ContainSingle();

        Step(0.1f);
        Step(0.1f);

        _finished.Should().ContainSingle();
        _world.Get<SpriteComponent>(entity).Frame.Should().Be(2);
    }

    [Fact]
    public void SpriteAnimationSystem_AStateThatLoops_StartsOverAndNeverFinishes()
    {
        Entity entity = Animate("walk");

        Step(0.1f);
        Step(0.1f);
        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(3);

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(0, "a state that loops starts over at its first frame");
        _world.Get<SpriteAnimationComponent>(entity).Paused.Should().BeFalse();
        _finished.Should().BeEmpty("a state that loops is never over");
    }

    [Fact]
    public void SpriteAnimationSystem_Speed_PlaysFramesFaster()
    {
        Entity entity = Animate("walk");
        _world.GetRef<SpriteAnimationComponent>(entity).Speed = 2f;

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(2, "twice the speed is twice the frames of a step");
    }

    [Fact]
    public void SpriteAnimationSystem_AStateTheSheetDoesNotDeclare_PlaysNothing()
    {
        Entity entity = Animate("jump");

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(0);
        _finished.Should().BeEmpty();
        _sheets.Missing.Should().Contain("goblin.yml:jump", "a state that a sheet does not declare is reported once");
    }

    [Fact]
    public void SpriteAnimationSystem_AnEntityWithoutASheet_PlaysNothingAndDoesNotFail()
    {
        Entity entity = _world.CreateEntity();
        _world.Set(entity, new SpriteComponent { Color = Color.White });
        _world.Set(entity, new SpriteAnimationComponent { State = "walk" });

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(0);
        _finished.Should().BeEmpty();
    }

    [Fact]
    public void SpriteAnimationSystem_AnotherState_IsDrawnFromItsFirstFrame()
    {
        // The prototype of the goblin names one state for what the sprite draws and another for what it plays: what plays is
        // what is drawn, and a state that takes over starts at its own first frame.
        Entity entity = _world.CreateEntity();
        _world.Set(entity, new SpriteComponent { SheetPath = "goblin.yml", State = "walk", Color = Color.White });
        _world.Set(entity, new SpriteAnimationComponent { State = "attack" });

        Step(0.1f);

        SpriteComponent sprite = _world.Get<SpriteComponent>(entity);

        sprite.State.Should().Be("attack", "the state that plays is the state that is drawn");
        sprite.Frame.Should().Be(1, "and it plays from the first frame of the state it took over");

        SpriteRegion region = _sheets.Resolve("goblin.yml", sprite.State!, sprite.Frame);

        region.Source.Y.Should().Be(1f / 3f, "the frame that is drawn lies on the row of the state of the attack");
    }

    [Fact]
    public void SpriteAnimationSystem_AStateThatDoesNotChange_KeepsItsProgress()
    {
        Entity entity = Animate("walk");

        Step(0.1f);
        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(2, "the play is only started over when the state changes");
    }

    [Fact]
    public void SpriteAnimationSystem_AStateThatCannotBePlayed_IsTheStateTheSpriteDraws()
    {
        Entity entity = Animate("walk");
        Step(0.1f);
        _world.GetRef<SpriteAnimationComponent>(entity).State = "jump";

        Step(0.1f);

        SpriteComponent sprite = _world.Get<SpriteComponent>(entity);

        sprite.State.Should().Be("jump", "the sprite does not go on drawing the state it was in");
        sprite.Frame.Should().Be(0, "and it stands on the first frame of the state it was asked for");
        _world.Get<SpriteAnimationComponent>(entity).Time.Should().Be(0f, "the play of the state it left does not carry into the new one");
        _finished.Should().BeEmpty();
        _sheets.Missing.Should().Contain("goblin.yml:jump");
        _sheets.Resolve("goblin.yml", sprite.State!, sprite.Frame).Texture.Should().Be(_textures.Error, "which a renderer answers with the placeholder for a state that is not there");
    }

    [Fact]
    public void SpriteAnimationSystem_AStateOfOneFrame_IsDrawnAndPlayedNoFurther()
    {
        Entity entity = Animate("walk");
        Step(0.1f);
        _world.GetRef<SpriteAnimationComponent>(entity).State = "idle";

        Step(0.1f);

        SpriteComponent sprite = _world.Get<SpriteComponent>(entity);

        sprite.State.Should().Be("idle");
        sprite.Frame.Should().Be(0, "a state of one frame stays on the frame it holds");
        _world.Get<SpriteAnimationComponent>(entity).Time.Should().Be(0f);
        _finished.Should().BeEmpty("a state of one frame never reaches a last frame that it could report");
        _sheets.Resolve("goblin.yml", "idle", 0).Source.Y.Should().Be(2f / 3f, "which is the row of the state on the sheet");
    }

    [Fact]
    public void SpriteAnimationSystem_AStateWithALengthPerFrame_PlaysEveryFrameOnItsOwnLength()
    {
        Entity entity = Animate("flurry");

        Step(0.05f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(1, "the first frame of the state lasts a twentieth of a second");

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(1, "the second frame lasts four times as long as the first");

        Step(0.1f);

        _world.Get<SpriteComponent>(entity).Frame.Should().Be(1, "the last frame of a state that does not loop stays on screen");
        _world.Get<SpriteAnimationComponent>(entity).Paused.Should().BeTrue("the play stands still until a game says what comes next");
        _finished.Should().ContainSingle("the state ends when the length of its last frame is over");
    }

    /// <summary>Puts a sprite of the sheet and an animation of it on a new entity.</summary>
    private Entity Animate(string state)
    {
        Entity entity = _world.CreateEntity();
        _world.Set(entity, new SpriteComponent { SheetPath = "goblin.yml", State = state, Color = Color.White });
        _world.Set(entity, new SpriteAnimationComponent { State = state });

        return entity;
    }

    /// <summary>Runs one step of the simulation and hands the events that it raised to their subscribers.</summary>
    private void Step(float seconds)
    {
        _system.Update(_world, new GameTime(seconds, 0d));
        _world.Events.Dispatch();
    }

    private sealed class FakeImageLoader : IImageLoader
    {
        public ImageData Load(string relativePath) => new(2, 2, new byte[16]);
    }

    /// <summary>A renderer that only uploads textures, which is what the texture service asks of one.</summary>
    private sealed class FakeRenderer : IRenderer
    {
        private int _created;

        public Vector2 ViewportSize => new(1280f, 720f);

        public void Attach(IWindowService window)
        {
        }

        public void SetCamera(Camera2D camera)
        {
        }

        public void BeginFrame(bool clear)
        {
        }

        public void DrawSprite(TextureHandle texture, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawTextureRegion(TextureHandle texture, Rect source, Vector2 position, Vector2 size, Color color, float rotation = 0f)
        {
        }

        public void DrawRectangle(Rect rect, Color color)
        {
        }

        public void DrawText(ReadOnlySpan<char> text, Vector2 position, Color color)
        {
        }

        public void EndFrame()
        {
        }

        public TextureHandle CreateTexture(ReadOnlySpan<byte> pixels, int width, int height) => new(++_created);

        public void ReleaseTexture(TextureHandle texture)
        {
        }

        public void Dispose()
        {
        }
    }
}
