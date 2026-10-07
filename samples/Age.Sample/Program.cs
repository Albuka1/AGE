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
IInputService input = provider.GetRequiredService<IInputService>();
IGameLoop gameLoop = provider.GetRequiredService<IGameLoop>();
SplashScreen splash = provider.GetRequiredService<SplashScreen>();

const float MoveSpeed = 240f;

gameLoop.Run(time =>
{
    if (splash.Draw(renderer, time, input))
    {
        return;
    }

    MoveFirstSprite(world, first, input, time);

    if (input.IsKeyPressed(Key.Escape))
    {
        gameLoop.Stop();
    }

    world.Update(time, pipeline);
    renderSystem.Render(world, camera);
    uiRenderSystem.Render(world);
});

windowService.Close();

static void MoveFirstSprite(World world, Entity entity, IInputService input, GameTime time)
{
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
