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

using ServiceProvider provider = services.BuildServiceProvider();

var world = new World();
SystemPipeline pipeline = provider.GetRequiredService<SystemPipeline>();
pipeline.Add(provider.GetRequiredService<CollisionSystem>());
pipeline.Add(provider.GetRequiredService<UIUpdateSystem>());

IWindowService windowService = provider.GetRequiredService<IWindowService>();
windowService.Create(1280, 720, "AGE Sample");

IRenderer renderer = provider.GetRequiredService<IRenderer>();
renderer.Attach(windowService);

var camera = new Camera2D
{
    Position = Vector2.Zero,
    Zoom = 1f,
    ViewportSize = new Vector2(1280f, 720f),
};

Entity first = world.CreateEntity();
world.Set(first, new TransformComponent { Position = new Vector2(400f, 300f), Scale = new Vector2(1f, 1f) });
world.Set(first, new SpriteComponent { Size = new Vector2(64f, 64f), Color = Color.Red, ZOrder = 0 });

Entity second = world.CreateEntity();
world.Set(second, new TransformComponent { Position = new Vector2(600f, 300f), Scale = new Vector2(1f, 1f) });
world.Set(second, new SpriteComponent { Size = new Vector2(64f, 64f), Color = Color.Green, ZOrder = 1 });

Entity panel = world.CreateEntity();
world.Set(panel, new RectTransformComponent { Position = new Vector2(100f, 100f), Size = new Vector2(200f, 50f), ZOrder = 0, Visible = true });
world.Set(panel, new ButtonComponent { BaseColor = Color.Blue, Interactable = true });
world.Set(panel, new TextLabelComponent { Text = "Hello AGE", Color = Color.White });

RenderSystem renderSystem = provider.GetRequiredService<RenderSystem>();
UIRenderSystem uiRenderSystem = provider.GetRequiredService<UIRenderSystem>();
IGameLoop gameLoop = provider.GetRequiredService<IGameLoop>();

gameLoop.Run(time =>
{
    world.Update(time, pipeline);
    renderSystem.Render(world, camera);
    uiRenderSystem.Render(world);
});

windowService.Close();
