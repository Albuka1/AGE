# Quickstart

## Create the service container

```csharp
var services = new ServiceCollection();
services.AddAgeCore();
services.AddAgeAssets();
services.AddAgeInput();
services.AddAgeAudio();
services.AddAgePhysics();
services.AddAgeUI();
services.AddAgeRendering();

using ServiceProvider provider = services.BuildServiceProvider();
```

## Create a window

```csharp
IWindowService windowService = provider.GetRequiredService<IWindowService>();
windowService.Create(1280, 720, "AGE Sample");

IRenderer renderer = provider.GetRequiredService<IRenderer>();
renderer.Attach(windowService);
```

## Build a world

```csharp
var world = new World();

Entity sprite = world.CreateEntity();
world.Set(sprite, new TransformComponent { Position = new Vector2(400, 300), Scale = new Vector2(1, 1) });
world.Set(sprite, new SpriteComponent { Size = new Vector2(64, 64), Color = Color.Red, ZOrder = 0 });
```

## Run the loop

```csharp
SystemPipeline pipeline = provider.GetRequiredService<SystemPipeline>();
pipeline.Add(provider.GetRequiredService<CollisionSystem>());

RenderSystem renderSystem = provider.GetRequiredService<RenderSystem>();
var camera = new Camera2D { Position = Vector2.Zero, Zoom = 1f, ViewportSize = new Vector2(1280, 720) };

provider.GetRequiredService<IGameLoop>().Run(time =>
{
    world.Update(time, pipeline);
    renderSystem.Render(world, camera);
});
```

`World` is never registered in the container; the caller owns its lifetime.

## Load an image

```csharp
IImageLoader images = provider.GetRequiredService<IImageLoader>();
ImageData logo = images.Load("logo.png");
```

`ImageData` holds tightly packed RGBA bytes with the origin at the top-left, so
the pixels can be handed to the renderer as they are.
