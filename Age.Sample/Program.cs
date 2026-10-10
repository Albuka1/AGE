using System.Globalization;
using Age.Assets;
using Age.Audio;
using Age.Content;
using Age.Content.Prototypes;
using Age.Core;
using Age.Input;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

// What the engine and this game report goes into the console of the developer overlay, which is what makes a file that
// failed to load or a device that refused something visible while the game runs. Logging is registered before
// `AddAgeCore`, because that call adds a logger that reports nothing only while nothing else is registered.
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Information);
    builder.Services.AddSingleton<ILoggerProvider>(provider => new ConsoleLoggerProvider(provider.GetRequiredService<IConsoleService>()));
});

services.AddAgeCore();
services.AddAgeContent();
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

// The systems that own the time of the game: a timer counts down and announces its end, a tween computes a value and
// announces it. Both are services of the container, like the collision system, and both run once per step.
pipeline.Add(provider.GetRequiredService<TimerSystem>());
pipeline.Add(provider.GetRequiredService<TweenSystem>());

// The animation of a sprite sheet plays on the same fixed steps: the goblin this game spawns from content walks because its
// prototype names a state of the sheet, not because this game counts frames.
pipeline.Add(provider.GetRequiredService<SpriteAnimationSystem>());

// The interface is a frame system, not a step system: the pointer is a state of the frame, and it keeps working while
// the simulation is paused, which is the whole point of a pause menu. The layout runs first: it is what turns the anchors of
// an element into the rectangle that the pointer test of the interface and the pass of the UI both read.
pipeline.AddFrame(provider.GetRequiredService<UILayoutSystem>());
pipeline.AddFrame(provider.GetRequiredService<UIUpdateSystem>());

IWindowService windowService = provider.GetRequiredService<IWindowService>();
windowService.Create(1280, 720, "AGE Sample");

IRenderer renderer = provider.GetRequiredService<IRenderer>();
renderer.Attach(windowService);

IAssetLoader assets = provider.GetRequiredService<IAssetLoader>();
assets.Initialize(Path.Combine(AppContext.BaseDirectory, "Resources"));

// The content of a game is data: a document under Resources/Prototypes declares a prototype by its identifier, names the
// components it carries with the values they start with, and inherits the rest from a parent. The manager reads every
// file first and then resolves them, so a mistake in a document is a message here rather than a surprise in a fight. A
// document whose type is `entity` is read as an entity, which is what a spawn makes out of it.
PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
SpawnService spawner = provider.GetRequiredService<SpawnService>();
prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);
prototypes.Register(MaterialPrototype.Kind, MaterialPrototype.Read);
Console.WriteLine($"Loaded {prototypes.Load(assets, "Prototypes")} prototypes.");

// A material is the stage of a shader and the values its uniforms start with, read from the content: a layer of a sprite names
// the material rather than the path of a stage, so a sprite that pulses is a line of a document rather than a line of code, and
// the same material draws every sprite that names it.
IMaterialService materials = provider.GetRequiredService<IMaterialService>();

foreach (MaterialPrototype material in prototypes.Enumerate<MaterialPrototype>())
{
    materials.Register(material.Id, material);
}

materials.Build();

ITextureService textures = provider.GetRequiredService<ITextureService>();

// A sprite names its image by path, which is the one thing about a sprite that survives a save: the handle of a device
// texture is never written to a scene. An image that a build does not ship is drawn as the placeholder of the texture
// service and reported once, rather than taking the frame down with it.
const string TilePath = "Textures/Tiles/tiles.bmp";

IFontService fonts = provider.GetRequiredService<IFontService>();
FontHandle font = fonts.Load("Fonts/Cousine-Regular.ttf", 24f, ' ', '\u04FF');

// A shader of the content: the fragment stage under Resources/Shaders is read through the asset loader, compiled once, and drawn
// with the vertex stage of the engine, which places the quad. This one is drawn over the frame at the end of it, which is what a
// pass of a game adds of its own.
IShaderService shaders = provider.GetRequiredService<IShaderService>();
ShaderHandle vignette = shaders.Load("Shaders/vignette.frag");

ISoundService sounds = provider.GetRequiredService<ISoundService>();
SoundHandle click = sounds.Load("Audio/Effects/click.wav");

// The area of the world that this game is authored against, in world units, which is also the resolution that its interface
// is authored against: the canvas of the interface scales it to the window, and `fit` of the console switches the camera of
// the world between one unit per pixel of the window, which shows more of the world in a larger window, and this whole area,
// which looks the same at every resolution. The command `ui` reports both of them while the game runs.
var design = new Vector2(1280f, 720f);
bool fitWorld = false;

Entity first = world.CreateEntity();
world.Set(first, new TransformComponent { Position = new Vector2(400f, 300f), Scale = new Vector2(1f, 1f) });
world.Set(first, new SpriteComponent { TexturePath = TilePath, Size = new Vector2(64f, 64f), Color = Color.White, ZOrder = 0 });
world.Set(first, new ColliderComponent { Size = new Vector2(64f, 64f) });

Entity second = world.CreateEntity();
world.Set(second, new TransformComponent { Position = new Vector2(430f, 315f), Scale = new Vector2(1f, 1f) });
world.Set(second, new SpriteComponent { Size = new Vector2(64f, 64f), Color = Color.Green, ZOrder = 1 });
world.Set(second, new ColliderComponent { Size = new Vector2(64f, 64f) });

// A sprite of layers: two images drawn over one another at the same box, the second of them with a shader of its own, which is
// what a layer is for. It is content like any other thing of the world, so the document names the layers; the pass of the world
// compiles the stage of a layer the first time it draws it and keeps the program for every frame after that.
spawner.Spawn(world, "Beacon", new Vector2(700f, 360f));

// The interface of this game is authored against the resolution above rather than against pixels of a screen: the canvas
// scales the whole of it to the window, and the elements below are anchored to the corners of that canvas instead of being put
// at a pixel, so the same layout fits a display of any resolution and a window of any shape. The command `ui` reports the scale
// that the layout resolved and changes the axis it follows while the game runs.
Entity canvas = world.CreateEntity();
world.Set(canvas, new CanvasComponent
{
    IsRoot = true,
    DesignSize = design,
    ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
    Match = 0.5f,
});

Entity panel = world.CreateEntity();
world.Set(panel, new RectTransformComponent
{
    // Anchored to the top-left corner of the canvas with a margin of its own, which is what keeps it there at any resolution.
    Anchored = true,
    AnchorMin = Vector2.Zero,
    AnchorMax = Vector2.Zero,
    Pivot = Vector2.Zero,
    AnchoredPosition = new Vector2(100f, 100f),
    SizeDelta = new Vector2(200f, 50f),
    ZOrder = 0,
    Visible = true,
});
world.Set(panel, new ButtonComponent { BaseColor = Color.Blue, Interactable = true });
world.Set(panel, new TextComponent
{
    // A label of the interface names a key and a count rather than a string, so a button says what the language says, and
    // its alignment is what centers the text in the box of the element.
    Key = "ui-entities",
    Count = 3,
    Style = new TextStyle { Color = Color.White, Align = TextAlign.Center, VerticalAlign = TextVerticalAlign.Middle },
});

// The other corner of the same canvas, with the opposite anchor and pivot: this element keeps its margin from the bottom-right
// corner of the window however the window is resized, which is what the anchors are for.
Entity corner = world.CreateEntity();
world.Set(corner, new RectTransformComponent
{
    Anchored = true,
    AnchorMin = new Vector2(1f, 1f),
    AnchorMax = new Vector2(1f, 1f),
    Pivot = new Vector2(1f, 1f),
    AnchoredPosition = new Vector2(-100f, -100f),
    SizeDelta = new Vector2(200f, 50f),
    ZOrder = 1,
    Visible = true,
});
world.Set(corner, new ButtonComponent { BaseColor = new Color(60, 90, 160), Interactable = true });
world.Set(corner, new TextComponent
{
    Text = "corner anchor",
    Style = new TextStyle { Color = Color.White, Align = TextAlign.Center, VerticalAlign = TextVerticalAlign.Middle },
});

RenderSystem renderSystem = provider.GetRequiredService<RenderSystem>();
TextRenderSystem textRenderSystem = provider.GetRequiredService<TextRenderSystem>();
UIRenderSystem uiRenderSystem = provider.GetRequiredService<UIRenderSystem>();
RenderPipeline renderPipeline = provider.GetRequiredService<RenderPipeline>();
renderPipeline.Add(renderSystem);

// The text of a world is a pass of its own, between the world and the interface: a line stands where the entity that carries
// it stands, so it belongs to the world, and it is drawn over the sprites and under the UI.
renderPipeline.Add(textRenderSystem);
renderPipeline.Add(uiRenderSystem);

// The vignette of this game is a pass like the ones of the engine, which is what the pipeline is for: it runs after the world
// and the interface and before the overlay, so the console and the numbers of the developer stay above it.
var vignettePass = new VignettePass(renderer, vignette);
renderPipeline.Add(vignettePass);

// The developer overlay draws over everything else: the numbers of the frame and the console of the engine. Its keys are
// Tab for the console, which pauses the simulation while it is open, and F1 for the numbers.
DevOverlay overlay = provider.GetRequiredService<DevOverlay>();

overlay.ConsoleKey = Key.Tab;
renderPipeline.Add(overlay);
IInputService input = provider.GetRequiredService<IInputService>();
IGameLoop gameLoop = provider.GetRequiredService<IGameLoop>();
SplashScreen splash = provider.GetRequiredService<SplashScreen>();
FixedTimestep timestep = provider.GetRequiredService<FixedTimestep>();

// The simulation does not start until the splash is over, so the clock starts paused: no step runs behind the logo, and
// the tick the HUD reports counts the simulation of this game rather than the frames of the splash.
timestep.Paused = true;

ISceneSerializer scenes = provider.GetRequiredService<ISceneSerializer>();
string scenePath = Path.Combine(AppContext.BaseDirectory, "scene.json");

if (File.Exists(scenePath))
{
    world = LoadScene(scenes, scenePath);
    first = FirstSprite(world);
    Console.WriteLine($"Loaded the scene from {scenePath}.");
}

// A bus belongs to a world, so every world this game makes needs its subscriptions again: the collision system
// announces each overlapping pair it found, the UI announces a press on a button, and a timer announces that it ran
// out. All of them save this game from looking for the same thing every step, and all of them run at the boundary of the
// step, which is where the world hands them over.
void SubscribeEvents(World subscribed)
{
    subscribed.Events.Subscribe<CollisionEvent>((_, collision) => TintByCollision(subscribed, collision.First, collision.Second));
    subscribed.Events.Subscribe<ButtonPressedEvent>((_, _) => sounds.Play(click));
    subscribed.Events.Subscribe<TimerElapsedEvent>((_, timer) => subscribed.RequestDestroy(timer.Entity));

    // The entity of an event is the one the event carries, because the world raises these without naming one to the
    // handler. A tween computes a value and announces it; where that value belongs is the business of the game, which
    // is what keeps the tween free of a delegate that a scene could neither save nor describe without reflection.
    subscribed.Events.Subscribe<TweenUpdatedEvent>((_, tween) =>
    {
        if (subscribed.IsAlive(tween.Entity) && subscribed.Has<TransformComponent>(tween.Entity))
        {
            subscribed.GetRef<TransformComponent>(tween.Entity).Rotation = tween.Value;
        }
    });
}

SubscribeEvents(world);

const float MoveSpeed = 240f;

// The consoles of the engine: a game registers the commands that belong to it and the settings it wants to turn while it
// runs. A setting becomes a command of its own, so `spawnLifetime 0.5` changes what this line below reads from then on.
IConsoleService console = provider.GetRequiredService<IConsoleService>();
CVarService cvars = provider.GetRequiredService<CVarService>();

cvars.Register("spawnLifetime", 2f, "How long a sprite that E puts on screen lives, in seconds.");

// The language the strings are read in is a setting rather than a way the game was built: the command 'loc' changes it while
// the game runs, and everything that asks the locale service for a key answers in the new language from then on. The language
// starts as the one the system is set to, when the game ships that language, and as the base language otherwise.
Age.Content.Locale.ILocaleService locale = provider.GetRequiredService<Age.Content.Locale.ILocaleService>();
cvars.Register("locale", locale.Language, "The language the strings of the game are read in, such as en or ru.");
locale.Language = cvars.Get<string>("locale");

// A line of text in the world, in the language the game plays in. The name of a prototype is a key rather than a text, so
// what a player reads is content: this line says the same thing as `locale.Get("ent-Goblin")` answers, and the `loc` command
// switches the language of a running game.
Entity label = world.CreateEntity();
world.Set(label, new TransformComponent { Position = new Vector2(300f, 180f), Scale = new Vector2(1f, 1f) });
world.Set(label, new TextComponent
{
    // The line names a key rather than a string, so it says what the language of the game says: the command `loc ru` turns
    // it into Russian without this game writing the name again.
    Key = "ent-Goblin",
    Style = new TextStyle
    {
        Fonts =
        [
            // The font is baked from the space to the end of the Cyrillic block, which is what a language with a script of
            // its own needs: a character outside the range of a font is drawn as a space.
            new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f, FirstCharacter = ' ', LastCharacter = '\u04FF' },
        ],
        Color = Color.White,
    },
    ZOrder = 10,
});

// The second sprite turns with a tween of three seconds that starts over when it reaches the end. Nothing in this game
// advances it: the tween system does, on the time of every step, and the subscription above writes the value into the
// transform of the entity it belongs to.
world.Set(second, TweenComponent.Between(0f, MathF.Tau, 3f, looping: true));

// Sprites that `E` puts on screen. A timer of two seconds on each one destroys it, so the slot of a destroyed entity is
// handed out again for the next one and this game keeps no book of its own. The string of the HUD remembers which entity
// was spawned last, with its generation.
Entity lastSpawned = default;

// The camera that the last frame was drawn through, which a console command reads: the camera of a frame is built in the
// callback that draws it, and a command runs at the boundary of a step, so the last one that was drawn is what the game is
// looking through when the command runs.
Camera2D drawnThrough = new() { Zoom = 1f };
string version = typeof(World).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

// One sprite is already on its way when the game starts, so the HUD has something to report and the slot logic runs
// before the first key press.
lastSpawned = Spawn(world, cvars.Get<float>("spawnLifetime"));

console.Register("spawn", "Puts a sprite on screen, the same as pressing E.", _ => lastSpawned = Spawn(world, cvars.Get<float>("spawnLifetime")));
console.Register("broken", "Puts a sprite whose image is not there on screen, which is what the ERROR placeholder is for.", _ => lastSpawned = SpawnMissing(world));
console.Register("stats", "Reports what the world holds, what it is missing and what the clock does.", _ => console.Write(
    $"entities {world.Enumerate().Count()}, tick {timestep.Tick}, {(timestep.Paused ? "paused" : "running")}, {collisions.LastPairs.Count} contacts, {textures.MissingCount} missing images"));

// A goblin of the content: the world receives exactly the components that the document declares, and this game names none
// of them. Where it stands is the one thing a spawn takes from the caller, because a map is what decides that.
console.Register("loc", "Reports the language the strings are read in, and switches to the one this names.", arguments =>
{
    if (arguments.Count > 0)
    {
        locale.Language = arguments[0];
    }

    console.Write($"language {locale.Language}, {locale.Count} strings, {locale.Missing.Count()} that did not resolve");
    console.Write($"goblin: {locale.Get("ent-Goblin")} / {locale.Get("ent-Goblin.desc")}");
    console.Write($"items: {locale.Get("ui-entities", ("count", 1))}, {locale.Get("ui-entities", ("count", 4))}");
});
console.Register("goblin", "Puts a goblin of the content in the world, at 320 by 240.", _ => spawner.Spawn(world, "Goblin", new Vector2(320f, 240f)));
console.Register("beacon", "Puts a sprite of two layers of the content in the world, at 700 by 360, the second layer of which is drawn with the pulse shader.", _ => spawner.Spawn(world, "Beacon", new Vector2(700f, 360f)));

// The shader of this game is a setting of the sample rather than of the engine: `vignette` turns it on and off while the game
// runs, which is what shows that the quads which were collected before a shader changes are drawn with the state they were
// collected under.
console.Register("vignette", "Turns the vignette of this game on and off, which is a pass of the content drawn over the frame.", _ =>
{
    vignettePass.Enabled = !vignettePass.Enabled;
    console.Write($"vignette {(vignettePass.Enabled ? "on" : "off")}");
});

// The scaler of the interface is content of this game rather than a decision of the engine, and it is resolved by the layout
// system on every frame: `ui` reports the scale the canvas ended up with and where the anchored elements were put, and `ui 0`,
// `ui 1` and `ui 0.5` change the axis the scale follows, which is what a person tuning a layout watches.
console.Register("ui", "Reports the scaler of the interface and the rectangle of every anchored element, and sets which axis the scale follows when it is given a value.", arguments =>
{
    Entity found = world.Enumerate<CanvasComponent>().FirstOrDefault();

    if (!world.IsAlive(found) || !world.Has<CanvasComponent>(found))
    {
        console.Write("this world holds no canvas, so the interface is measured against the window itself");
        return;
    }

    ref CanvasComponent canvasComponent = ref world.GetRef<CanvasComponent>(found);

    if (arguments.Count > 0 && float.TryParse(arguments[0], CultureInfo.InvariantCulture, out float match))
    {
        canvasComponent.Match = match;
    }

    console.Write($"canvas {canvasComponent.Resolution.X:0}x{canvasComponent.Resolution.Y:0} design units at scale {canvasComponent.Scale:0.###}, match {canvasComponent.Match:0.##}, window {renderer.ViewportSize.X:0}x{renderer.ViewportSize.Y:0}");

    foreach (Entity element in world.Enumerate<RectTransformComponent>())
    {
        RectTransformComponent rect = world.Get<RectTransformComponent>(element);

        if (rect.Anchored)
        {
            console.Write($"  anchored from {rect.AnchorMin.X:0.#},{rect.AnchorMin.Y:0.#} to {rect.AnchorMax.X:0.#},{rect.AnchorMax.Y:0.#} sits at {rect.Position.X:0},{rect.Position.Y:0} with a size of {rect.Size.X:0}x{rect.Size.Y:0}");
        }
    }
});

// The world of this game is drawn one unit per pixel by default, so a larger window shows more of it. `G` is the other way
// round: the camera is built from the area this game is authored against, so a window of any shape shows the same view of the
// world, cropped rather than stretched where the shapes differ. The HUD reports which of the two is in use.
console.Register("point", "Reports where the pointer is on the screen and in the world, and where that world position is drawn.", _ =>
{
    // The camera of the frame that was last drawn, which is the one the player is looking through now. The conversion is what a
    // click asks before it decides what stands where: the pointer is a point of the screen and a cell of a map is a box of the
    // world, and the two meet here.
    Vector2 screen = input.MousePosition;
    Vector2 world = drawnThrough.ScreenToWorld(screen);
    Vector2 back = drawnThrough.WorldToScreen(world);

    console.Write($"pointer {screen.X:0}x{screen.Y:0} on screen is {world.X:0}x{world.Y:0} in the world, and that is drawn back at {back.X:0}x{back.Y:0}");
});

console.Register("fit", "Reports how the camera of the world is built and switches it to the other of the two ways.", _ =>
{
    fitWorld = !fitWorld;

    Camera2D fitted = Camera2D.Fit(design, renderer.ViewportSize, CameraFit.Cover);
    Vector2 view = fitWorld ? fitted.VisibleWorld.Size : renderer.ViewportSize;

    console.Write($"world {(fitWorld ? "fitted" : "one unit per pixel")}: window {renderer.ViewportSize.X:0}x{renderer.ViewportSize.Y:0} shows {view.X:0}x{view.Y:0} units of the world");
});

// The simulation runs in fixed steps, so movement, collision and the UI advance by the same amount on every frame at any
// frame rate. The splash, the keys and the drawing run once per frame, after the steps of that frame, and the simulation
// stays paused until the splash is over, so nothing moves behind the logo. `Q` pauses the clock, which stops the steps
// without stopping the frames, and holding `Tab` slows the simulation down with `FixedTimestep.TimeScale`.
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

        // A contact is announced by `CollisionEvent`, but a pair that came apart raises nothing: the collision component
        // of the sprite is gone and the event was about the contact, so the calm colour goes back on here, every step.
        RestoreCalmColour(world, first, Color.White);
        RestoreCalmColour(world, second, Color.Green);
    },
    render: time =>
    {
        // The frame systems run on the time of this frame, whether or not the simulation advanced, so the interface and
        // the overlays of this game keep working while the clock is paused.
        world.UpdateFrame(time, pipeline);

        // The overlay reads the keys of this frame before the passes draw, which is what opens the console and runs a line.
        overlay.Update(time);

        if (splash.Draw(renderer, time, input))
        {
            return;
        }

        started = true;

        // The splash is over, so the clock runs: from here the tick of the HUD counts the steps of this game.
        timestep.Paused = false;

        if (input.IsKeyPressed(Key.Q))
        {
            timestep.Paused = !timestep.Paused;
        }

        // Held rather than pressed: the factor is a live state of the clock, so holding the key slows the world down and
        // letting go brings it back to normal speed. Tab belongs to the console of the overlay.
        timestep.TimeScale = input.IsKeyDown(Key.ControlLeft) ? 0.25d : 1d;

        if (input.IsKeyPressed(Key.F))
        {
            File.WriteAllText(scenePath, scenes.Save(world));
            Console.WriteLine($"Saved the scene to {scenePath}.");
        }

        if (input.IsKeyPressed(Key.R) && File.Exists(scenePath))
        {
            world = LoadScene(scenes, scenePath);
            first = FirstSprite(world);
            SubscribeEvents(world);
            Console.WriteLine("Loaded the scene again.");
        }

        if (input.IsKeyPressed(Key.E))
        {
            lastSpawned = Spawn(world, cvars.Get<float>("spawnLifetime"));
        }

        // The other way of drawing the world: the camera is built from the design area of this game rather than from the pixels
        // of the window, so a window of any shape shows the same view of the world and crops what does not fit instead of
        // stretching it. The line reports the area that is in view, which is what a person tuning a design wonders about.
        if (input.IsKeyPressed(Key.G))
        {
            fitWorld = !fitWorld;

            Camera2D fitted = Camera2D.Fit(design, renderer.ViewportSize, CameraFit.Cover);
            Vector2 view = fitWorld ? fitted.VisibleWorld.Size : renderer.ViewportSize;

            Console.WriteLine($"The world is {(fitWorld ? "fitted to the design area" : "one unit per pixel")}: window {renderer.ViewportSize.X:0}x{renderer.ViewportSize.Y:0} shows {view.X:0}x{view.Y:0} units of it.");
        }

        if (input.IsKeyPressed(Key.Escape))
        {
            gameLoop.Stop();
        }

        // The camera takes the size of the frame before the passes run, so culling and the projection of the renderer
        // agree, including after the window was resized. `fit` switches it between one unit per pixel of the window and the
        // area this game is authored against, which is what a game that must look the same on every display does.
        Camera2D camera = fitWorld
            ? Camera2D.Fit(design, renderer.ViewportSize, CameraFit.Cover)
            : new Camera2D { Position = Vector2.Zero, Zoom = 1f, ViewportSize = renderer.ViewportSize };

        drawnThrough = camera;

        renderPipeline.Render(world, camera);

        // Text of this game, baked from the TrueType font in Resources/Fonts. The UI pass above draws with the built-in
        // bitmap font, so both are visible in the same frame. The lines are drawn in a frame of this game's own, because a
        // frame is what puts what it holds on screen: a draw that no frame opened lands in the frame after it, under
        // everything of that one, which is not what a line that reports the state of a frame wants.
        renderer.BeginFrame(false);

        string spawnText = lastSpawned == default
            ? "nothing spawned yet"
            : $"last spawn is entity {lastSpawned.Id}, generation {lastSpawned.Generation}, {(world.IsAlive(lastSpawned) ? "alive" : "destroyed")}";

        // The clock describes the simulation: the tick counts the steps that ran, and it stands still while the game is
        // paused, even though the frames keep coming.
        string clockText = timestep.Paused
            ? $"paused at tick {timestep.Tick}"
            : $"tick {timestep.Tick} at {timestep.TimeScale:0.##}x";

        fonts.Draw(font, $"AGE {version} - WASD move, E spawn, click the panel for a sound, F save, R load, Q pause, Ctrl slow motion, G world fit, Tab console, F1 numbers", new Vector2(24f, 24f), Color.White);
        fonts.Draw(font, $"entities {world.Enumerate().Count()}, contacts {collisions.LastPairs.Count}, prototypes {prototypes.Count}, missing images {textures.MissingCount}, {clockText}, {spawnText}", new Vector2(24f, 56f), Color.White);

        // The size of the frame and the scale that the canvas of the interface resolved for it, which is what makes the effect
        // of a resize visible without opening the console: `ui` and `fit` report the same numbers line by line. The lines of
        // this HUD are drawn at pixels of the window rather than through the canvas, because they are numbers of a developer
        // rather than an interface of the game.
        Entity canvasOf = world.Enumerate<CanvasComponent>().FirstOrDefault();
        CanvasComponent canvasState = world.Has<CanvasComponent>(canvasOf) ? world.Get<CanvasComponent>(canvasOf) : default;

        fonts.Draw(font, $"window {renderer.ViewportSize.X:0}x{renderer.ViewportSize.Y:0}, UI scale {canvasState.Scale:0.###} of {design.X:0}x{design.Y:0}, world {(fitWorld ? "fitted to the design area" : "one unit per pixel")}, canvas {canvasState.Resolution.X:0}x{canvasState.Resolution.Y:0} design units", new Vector2(24f, 152f), new Color(255, 220, 120));

        // A count of things is a string of the content rather than a number this game writes: Russian writes three forms of it
        // where English writes two, so the line says what the language says. `loc ru` changes it while the game runs.
        fonts.Draw(font, locale.Get("ui-entities", ("count", world.Enumerate().Count())), new Vector2(24f, 88f), Color.White);
        fonts.Draw(font, $"language {locale.Language} of {string.Join(", ", locale.Languages)}, {locale.Count} strings, {locale.Missing.Count()} that did not resolve", new Vector2(24f, 120f), new Color(255, 220, 120));

        renderer.EndFrame();
    });

// The device objects live in the OpenGL context of the window, so the game releases them while the window is still open.
// One call runs every step of the shutdown in the order the engine registered: the splash and what it uploaded, the
// atlases and the textures of this frame, the samples of the sound device, the renderer, and the window itself last. The
// container disposes the services when it goes out of scope, and every one of those calls is a no-op by then.
provider.GetRequiredService<GameShutdown>().Run();

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

// The entity that the keyboard moves: the first sprite of the world, which is the one the demo creates first and the one
// a loaded scene brings back. Nothing of a loaded sprite is put back by hand, because a sprite carries the path of its
// image and the renderer asks the texture service for it.
static Entity FirstSprite(World world) => world.Enumerate<SpriteComponent>().FirstOrDefault();

// Puts a short-lived sprite on screen, with a timer that destroys it when the time is up. This game does not count the
// seconds of a spawned sprite and does not keep a list of them: the timer system advances every timer of the world, and
// the subscription above destroys the entity of the one that ran out. The slot it used was handed out before, so the
// identifier of the entity that used it comes back with a new generation.
static Entity Spawn(World world, float lifetime)
{
    Entity entity = world.CreateEntity();

    world.Set(entity, new TransformComponent { Position = new Vector2(200f + ((entity.Id * 37f) % 800f), 560f), Scale = new Vector2(1f, 1f) });
    world.Set(entity, new SpriteComponent { Size = new Vector2(32f, 32f), Color = Color.Blue, ZOrder = 2 });
    world.Set(entity, TimerComponent.For(lifetime));

    return entity;
}

// Puts a sprite whose image is not there on screen. Content that names an image a build does not ship is exactly the
// mistake that the placeholder exists for: the sprite is drawn as a bright ERROR checkerboard, one line of the log names
// the file, the number of missing images stays visible in the HUD, and the game keeps running.
static Entity SpawnMissing(World world)
{
    Entity entity = world.CreateEntity();

    world.Set(entity, new TransformComponent { Position = new Vector2(640f, 200f), Scale = new Vector2(1f, 1f) });
    world.Set(entity, new SpriteComponent { TexturePath = "Textures/Nowhere/gone.png", Size = new Vector2(96f, 96f), Color = Color.White, ZOrder = 3 });
    world.Set(entity, TimerComponent.For(6f));

    return entity;
}

// Puts the calm colour back on a sprite that is not touching anything any more. A contact is announced as long as it
// exists, so the sprite of a pair that came apart would keep its touching colour without this.
static void RestoreCalmColour(World world, Entity entity, Color calm)
{
    if (!world.IsAlive(entity) || !world.Has<SpriteComponent>(entity) || world.Has<CollisionComponent>(entity))
    {
        return;
    }

    ref SpriteComponent sprite = ref world.GetRef<SpriteComponent>(entity);
    sprite.Color = calm;
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

// Draws the vignette of this game over the frame, which is what a post-process of a game looks like: a quad in pixels covers the
// frame, and the fragment stage of the content reads the surface that the quad is drawn into — the world, the text and the
// interface of this game as they stand — and writes it back with its edges darkened. It runs after the world and the interface
// and before the overlay, so the console and the numbers of the developer stay above it.
internal sealed class VignettePass(IRenderer renderer, ShaderHandle vignette) : IRenderPass
{
    // Whether the pass draws the vignette, which the console of this game turns off and on while it runs.
    public bool Enabled { get; set; } = true;

    public void Render(World world, in Camera2D camera)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!Enabled)
        {
            return;
        }

        // A camera that maps one unit to one pixel of the frame: what this pass covers is the frame rather than the world.
        var screenCamera = new Camera2D { Position = Vector2.Zero, Zoom = 1f, ViewportSize = renderer.ViewportSize };

        // The engine copies the surface into a texture when a shader declares the sampler of it, which is what this call does
        // for the stage below: what it reads is the frame of this game as it stands when this pass runs, so the vignette is a
        // picture of the frame with its edges darkened rather than a colour over it. The two settings are of this game.
        renderer.SetCamera(screenCamera);
        renderer.BeginFrame(false);
        renderer.UseShader(vignette);
        renderer.SetUniform("uVignetteColour", [0.16f, 0.20f, 0.38f]);
        renderer.SetUniform("uVignetteStrength", 0.85f);
        renderer.DrawRectangle(new Rect(Vector2.Zero, renderer.ViewportSize), Color.White);
        renderer.ResetShader();
        renderer.EndFrame();
    }
}
