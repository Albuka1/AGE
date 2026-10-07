using Age.Assets;
using Age.Audio;
using Age.Core;
using Age.Input;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAgeCore();
services.AddAgeAssets();
services.AddAgeInput();
services.AddAgeAudio();
services.AddAgeOpenALAudio();
services.AddAgePhysics();
services.AddAgeUI();
services.AddAgeRendering();
services.AddAgeSilkInput();

using ServiceProvider provider = services.BuildServiceProvider();

var world = new World();
SystemPipeline pipeline = provider.GetRequiredService<SystemPipeline>();
pipeline.Add(provider.GetRequiredService<CollisionSystem>());
pipeline.Add(provider.GetRequiredService<UIUpdateSystem>());

IWindowService windowService = provider.GetRequiredService<IWindowService>();
windowService.Create(1280, 720, "AGE Sample");

IRenderer renderer = provider.GetRequiredService<IRenderer>();
renderer.Attach(windowService);

IAssetLoader assets = provider.GetRequiredService<IAssetLoader>();
assets.Initialize(Path.Combine(AppContext.BaseDirectory, "content"));

ITextureService textures = provider.GetRequiredService<ITextureService>();
TextureHandle tiles = textures.Load("tiles.bmp");

ISoundService sounds = provider.GetRequiredService<ISoundService>();
SoundHandle click = sounds.Load("click.wav");

var camera = new Camera2D
{
    Position = Vector2.Zero,
    Zoom = 1f,
    ViewportSize = new Vector2(1280f, 720f),
};

Entity first = world.CreateEntity();
world.Set(first, new TransformComponent { Position = new Vector2(400f, 300f), Scale = new Vector2(1f, 1f) });
world.Set(first, new SpriteComponent { Texture = tiles, Size = new Vector2(64f, 64f), Color = Color.White, ZOrder = 0 });

Entity second = world.CreateEntity();
world.Set(second, new TransformComponent { Position = new Vector2(600f, 300f), Scale = new Vector2(1f, 1f) });
world.Set(second, new SpriteComponent { Size = new Vector2(64f, 64f), Color = Color.Green, ZOrder = 1 });

Entity panel = world.CreateEntity();
world.Set(panel, new RectTransformComponent { Position = new Vector2(100f, 100f), Size = new Vector2(200f, 50f), ZOrder = 0, Visible = true });
world.Set(panel, new ButtonComponent { BaseColor = Color.Blue, Interactable = true });
world.Set(panel, new TextLabelComponent { Text = "Hello AGE", Color = Color.White });

RenderSystem renderSystem = provider.GetRequiredService<RenderSystem>();
UIRenderSystem uiRenderSystem = provider.GetRequiredService<UIRenderSystem>();
RenderPipeline renderPipeline = provider.GetRequiredService<RenderPipeline>();
renderPipeline.Add(renderSystem);
renderPipeline.Add(uiRenderSystem);
IInputService input = provider.GetRequiredService<IInputService>();
IGameLoop gameLoop = provider.GetRequiredService<IGameLoop>();
SplashScreen splash = provider.GetRequiredService<SplashScreen>();

ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
string scenePath = Path.Combine(AppContext.BaseDirectory, "scene.json");

if (File.Exists(scenePath))
{
    world = LoadScene(scenes, scenePath);
    first = MoveTarget(world, tiles);
    Console.WriteLine($"Loaded the scene from {scenePath}.");
}

const float MoveSpeed = 240f;

// The simulation runs in fixed steps, so movement, collision and the UI advance by the same amount on every frame at any
// frame rate. The splash, the keys and the drawing run once per frame, after the steps of that frame, and the simulation
// stays paused until the splash is over, so nothing moves behind the logo.
bool started = false;

gameLoop.Run(
    update: step =>
    {
        if (!started)
        {
            return;
        }

        MoveFirstSprite(world, first, input, step);
        world.Update(step, pipeline);
    },
    render: time =>
    {
        if (splash.Draw(renderer, time, input))
        {
            return;
        }

        started = true;

        if (input.IsKeyPressed(Key.Space))
        {
            sounds.Play(click);
        }

        if (input.IsKeyPressed(Key.F))
        {
            File.WriteAllText(scenePath, scenes.Save(world));
            Console.WriteLine($"Saved the scene to {scenePath}.");
        }

        if (input.IsKeyPressed(Key.R) && File.Exists(scenePath))
        {
            world = LoadScene(scenes, scenePath);
            first = MoveTarget(world, tiles);
            Console.WriteLine("Loaded the scene again.");
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            gameLoop.Stop();
        }

        renderPipeline.Render(world, camera);
    });

windowService.Close();

static void MoveFirstSprite(World world, Entity entity, IInputService input, GameTime time)
{
    if (!world.IsAlive(entity) || !world.Has<TransformComponent>(entity))
    {
        return;
    }

    float step = (float)(time.Delta * MoveSpeed);
    Vector2 offset = Vector2.Zero;

    if (input.IsKeyDown(Key.W))
    {
        offset.Y -= step;
    }

    if (input.IsKeyDown(Key.S))
    {
        offset.Y += step;
    }

    if (input.IsKeyDown(Key.A))
    {
        offset.X -= step;
    }

    if (input.IsKeyDown(Key.D))
    {
        offset.X += step;
    }

    if (offset != Vector2.Zero)
    {
        ref TransformComponent transform = ref world.GetRef<TransformComponent>(entity);
        transform.Position += offset;
    }
}

// Loading replaces the scene: a fresh world keeps the file and the screen in step, instead of piling the entities of
// the file on top of the ones that are already there.
static World LoadScene(ISceneSerializer scenes, string path)
{
    var loaded = new World();
    scenes.Load(loaded, File.ReadAllText(path));
    return loaded;
}

// The entity that the keyboard moves: the first sprite of the world, which is the one the demo creates first and the
// one a loaded scene brings back. A texture handle does not survive a save, so the sprite is pointed at the texture
// that this run loaded; a scene without a sprite leaves nothing to point.
static Entity MoveTarget(World world, TextureHandle texture)
{
    Entity entity = world.Enumerate<SpriteComponent>().FirstOrDefault();

    if (!world.IsAlive(entity) || !world.Has<SpriteComponent>(entity))
    {
        return entity;
    }

    ref SpriteComponent sprite = ref world.GetRef<SpriteComponent>(entity);
    sprite.Texture = texture;
    return entity;
}
