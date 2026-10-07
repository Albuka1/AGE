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
CollisionSystem collisions = provider.GetRequiredService<CollisionSystem>();
pipeline.Add(collisions);
pipeline.Add(provider.GetRequiredService<UIUpdateSystem>());

IWindowService windowService = provider.GetRequiredService<IWindowService>();
windowService.Create(1280, 720, "AGE Sample");

IRenderer renderer = provider.GetRequiredService<IRenderer>();
renderer.Attach(windowService);

IAssetLoader assets = provider.GetRequiredService<IAssetLoader>();
assets.Initialize(Path.Combine(AppContext.BaseDirectory, "content"));

ITextureService textures = provider.GetRequiredService<ITextureService>();
TextureHandle tiles = textures.Load("tiles.bmp");

IFontService fonts = provider.GetRequiredService<IFontService>();
FontHandle font = fonts.Load("fonts/Cousine-Regular.ttf", 24f);

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
world.Set(first, new ColliderComponent { Size = new Vector2(64f, 64f) });

Entity second = world.CreateEntity();
world.Set(second, new TransformComponent { Position = new Vector2(430f, 315f), Scale = new Vector2(1f, 1f) });
world.Set(second, new SpriteComponent { Size = new Vector2(64f, 64f), Color = Color.Green, ZOrder = 1 });
world.Set(second, new ColliderComponent { Size = new Vector2(64f, 64f) });

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
const float SpawnLifetime = 2f;

// Sprites that `E` puts on screen and that are destroyed a couple of seconds later, so the slot of a destroyed entity
// is handed out again for the next one. The string of the HUD remembers which entity that was, with its generation.
var spawned = new List<(Entity Entity, float Age)>();
Entity lastSpawned = default;
string version = typeof(World).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

// One sprite is already on its way when the game starts, so the HUD has something to report and the slot logic runs
// before the first key press.
lastSpawned = Spawn(world, spawned);

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
        SpinSprite(world, second, step);
        AgeSpawned(world, spawned, step.Delta, SpawnLifetime);
        world.Update(step, pipeline);

        // The collision system resolved the contacts of this step, so the sprites show whether they touch right now.
        TintByCollision(world, first, second);
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

        if (input.IsKeyPressed(Key.E))
        {
            lastSpawned = Spawn(world, spawned);
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            gameLoop.Stop();
        }

        // The camera takes the size of the frame before the passes run, so culling and the projection of the renderer
        // agree, including after the window was resized.
        camera.ViewportSize = renderer.ViewportSize;
        renderPipeline.Render(world, camera);

        // Text of this game, baked from the TrueType font in content/fonts. The UI pass above draws with the built-in
        // bitmap font, so both are visible in the same frame.
        string spawnText = lastSpawned == default
            ? "nothing spawned yet"
            : $"last spawn is entity {lastSpawned.Id}, generation {lastSpawned.Generation}, {(world.IsAlive(lastSpawned) ? "alive" : "destroyed")}";

        fonts.Draw(font, $"AGE {version} - WASD to move, E to spawn, Space to play, F to save, R to load", new Vector2(24f, 24f), Color.White);
        fonts.Draw(font, $"entities {world.Enumerate().Count()}, contacts {collisions.LastPairs.Count}, {spawnText}", new Vector2(24f, 56f), Color.White);
    });

// The device objects live in the OpenGL context of the window, so the game releases them while the window is still open:
// the logo of the splash, the textures that were loaded, and finally the renderer. The container disposes the services
// when it goes out of scope, and every one of those calls is a no-op by then.
splash.Dispose();
fonts.UnloadAll();
textures.UnloadAll();
renderer.Dispose();
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

// Spins the second sprite: the renderer reads the rotation of a transform and turns the quad around its centre.
static void SpinSprite(World world, Entity entity, GameTime time)
{
    if (!world.IsAlive(entity) || !world.Has<TransformComponent>(entity))
    {
        return;
    }

    ref TransformComponent transform = ref world.GetRef<TransformComponent>(entity);
    transform.Rotation += (float)(time.Delta * 1.4d);
}

// Ages the spawned sprites and destroys the ones that lived long enough.
static void AgeSpawned(World world, List<(Entity Entity, float Age)> spawned, double delta, float lifetime)
{
    for (int index = spawned.Count - 1; index >= 0; index--)
    {
        (Entity entity, float age) = spawned[index];

        if (!world.IsAlive(entity))
        {
            spawned.RemoveAt(index);
            continue;
        }

        age += (float)delta;
        if (age >= lifetime)
        {
            world.DestroyEntity(entity);
            spawned.RemoveAt(index);
            continue;
        }

        spawned[index] = (entity, age);
    }
}

// Puts a short-lived sprite on screen. The slot it uses was handed out before, so the identifier of the entity that
// used it comes back with a new generation.
static Entity Spawn(World world, List<(Entity Entity, float Age)> spawned)
{
    Entity entity = world.CreateEntity();

    world.Set(entity, new TransformComponent { Position = new Vector2(200f + ((spawned.Count * 37f) % 800f), 560f), Scale = new Vector2(1f, 1f) });
    world.Set(entity, new SpriteComponent { Size = new Vector2(32f, 32f), Color = Color.Blue, ZOrder = 2 });
    spawned.Add((entity, 0f));

    return entity;
}

// Tints a sprite while its collider touches another one, so the contacts of the collision system are visible.
static void TintByCollision(World world, Entity first, Entity second)
{
    Tint(world, first, Color.White, Color.Blue);
    Tint(world, second, Color.Green, Color.Red);

    static void Tint(World world, Entity entity, Color calm, Color touching)
    {
        if (!world.IsAlive(entity) || !world.Has<SpriteComponent>(entity))
        {
            return;
        }

        ref SpriteComponent sprite = ref world.GetRef<SpriteComponent>(entity);
        sprite.Color = world.Has<CollisionComponent>(entity) ? touching : calm;
    }
}
