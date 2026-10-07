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

## Replace the window icon

```csharp
IImageLoader images = provider.GetRequiredService<IImageLoader>();
ImageData icon = images.Load("icon.png");

windowService.SetIcon(icon.Pixels, icon.Width, icon.Height);
```

A window opens with the icon of the engine, so a game can leave it as it is. `SetIcon` swaps it
for an image of the game's own: it takes decoded pixels rather than a path, so the call stays free
of any file format.

## Build a world

```csharp
var world = new World();

Entity sprite = world.CreateEntity();
world.Set(sprite, new TransformComponent { Position = new Vector2(400, 300), Scale = new Vector2(1, 1) });
world.Set(sprite, new SpriteComponent { Size = new Vector2(64, 64), Color = Color.Red, ZOrder = 0 });
```

## Save and load a scene

```csharp
ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();

string json = scenes.Save(world);
scenes.Load(world, json);
```

A scene holds every entity whose components are registered, keyed by the name they were registered
under, and it is written through the source generated contracts of the assemblies, so it works in an
AOT build. `AddAgeCore` builds the registry from every `IComponentRegistrations` in the container,
which is how each assembly contributes its own components. Register one of your own to make a
component of a game part of a scene:

```csharp
internal sealed class GameComponentRegistrations : IComponentRegistrations
{
    public void Register(ComponentRegistry registry) =>
        registry.Register("Health", GameJsonContext.Default.HealthComponent);
}
```

The implementations are collected when the container is built, so register yours with the rest of the
services:

```csharp
services.AddSingleton<IComponentRegistrations, GameComponentRegistrations>();
```

A component that refers to a device resource, such as the texture of a `SpriteComponent`, is written
as the identifier it carried, and that identifier does not survive a reload: load the texture again
and set it on the component after the scene was loaded.

## Load an asset

```csharp
IAssetLoader assets = provider.GetRequiredService<IAssetLoader>();
assets.Initialize("content");

TransformComponent spawn = assets.Load<TransformComponent>("spawn.json");
string text = assets.Load<string>("readme.txt");
byte[] bytes = assets.Load<byte[]>("logo.bin");
```

`Load<T>` reads UTF-8 text into `string`, the raw bytes into `byte[]` and
deserializes every other type from JSON, so a component authored as
`{ "position": { "x": 120, "y": 64 } }` binds to its public fields. Every path
stays inside the game root that `Initialize` was given, and an escaping path
throws.

## Load an image

```csharp
IImageLoader images = provider.GetRequiredService<IImageLoader>();
ImageData logo = images.Load("logo.png");
```

`ImageData` holds tightly packed RGBA bytes with the origin at the top-left, so
the pixels can be handed to the renderer as they are.

## Draw a texture

```csharp
ITextureService textures = provider.GetRequiredService<ITextureService>();
TextureHandle playerTexture = textures.Load("art/player.png");

world.Set(sprite, new SpriteComponent { Texture = playerTexture, Size = new Vector2(64, 64), Color = Color.White });
```

`ITextureService` decodes the file once, uploads it and caches it by path, so loading the same
image twice returns the same texture. Release it with `Unload` when the level that used it ends.

## Load and play a sound

```csharp
ISoundService sounds = provider.GetRequiredService<ISoundService>();
SoundHandle click = sounds.Load("sfx/click.wav");

sounds.Play(click, volume: 0.8f);
```

`ISoundService` decodes the file through `ISoundLoader`, uploads it to the audio device and caches
it by path, so loading the same sound twice returns the same one. The loader reads the header of the
file, so WAVE, MP3 and Ogg Vorbis all work and the file name does not have to say which one it is.
Playing a handle the game does not own, or one it unloaded, plays nothing, and looping playback
stops with `StopAll`.

The default device discards every sound, which is what a headless run wants. Register the OpenAL
device after `AddAgeAudio` to hear them:

```csharp
services.AddAgeAudio();          // the sound resources and the null device
services.AddAgeOpenALAudio();    // the device that plays, where the machine has one
```

## Show a splash screen

```csharp
SplashScreen splash = provider.GetRequiredService<SplashScreen>();

gameLoop.Run(time =>
{
    if (splash.Draw(renderer, time))
    {
        return;
    }

    world.Update(time, pipeline);
    renderSystem.Render(world, camera);
});
```

`Draw` keeps the frame for 2.5 seconds and returns `false` once it is over, so the game draws
nothing until then. A logo without a background suits it, because the screen behind it is cleared
to black. Pass the input service as the third argument to let Space, Enter, Escape or a click end
the splash early, and call `splash.End` from the game to start it as soon as its assets are loaded.

```csharp
ITextureService textures = provider.GetRequiredService<ITextureService>();
var own = new SplashScreen { Logo = textures.Load("art/logo.png"), LogoSize = new Vector2(512, 512) };
var none = new SplashScreen { Enabled = false };
```

A `SplashScreen` without either setting shows the built-in logo of the engine, and one with
`Enabled` set to `false` starts the game straight away.

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
