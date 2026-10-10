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

The identifier of a destroyed entity is handed out again by the next `CreateEntity`, in a new generation, so
`IsAlive` tells an identifier that outlived its entity from a live one. `Entity` compares by slot and generation, which
makes it safe to keep in a component.

The storage of a slot is handed out again with the slot, so a `ref` from `GetRef` that was held across the destruction
of its entity points at the component of the next entity that takes the slot. Take the reference immediately before the
write, or use `Borrow`, a `ref struct` that validates the entity on every access:

```csharp
ComponentRef<TransformComponent> transform = world.Borrow<TransformComponent>(sprite);

TransformComponent value = transform.Value;
value.Position += new Vector2(10, 0);
transform.Value = value;
```

A borrow cannot be stored in a field or a collection, which is what keeps it from outliving the frame that took it.

A `World` is not thread-safe. It belongs to the thread that runs the game loop, and everything that changes it comes from
the update callback of that loop; a game that touches one world from more than one thread serializes the calls itself.
The generation checks of `IsAlive`, `Borrow` and `GetRef` are correctness for that sequential use — they keep an
identifier or a borrow that outlived its entity from reaching the component of the entity that took the slot — and they
are not a concurrency guarantee, because validation and access are two separate steps.

A world cannot be changed while one of its sequences is being enumerated: a component that appears or disappears, and
an entity that is created or destroyed, all throw once the sequence notices. Collect the entities into a list first, or
request the change, which is applied at the end of the step:

```csharp
foreach (Entity entity in world.Enumerate<TransformComponent>())
{
    world.RequestDestroy(entity);   // gone at the end of the step, while this loop runs to its end
}

world.ApplyPending();   // the loop does this itself, before and after the systems of the step
```

An entity whose destruction was requested is not alive for the rest of the step, so `IsAlive` and `Has<T>` already report
it as gone, while its components stay in place until the queue is applied. Neither `Enumerate` nor `Enumerate<T>` visits
it. `RequestCreate` works the same way for spawning: the identifier is reserved right away and the entity joins the world
when the queue is applied. Writing over a component that is already there is allowed, and so is changing a component type
that the sequence you are walking does not look at.

A system can react instead of looking for something every step: the world raises events for its own changes, and the
systems of the engine raise theirs.

```csharp
world.Events.Subscribe<CollisionEvent>((_, collision) => Console.WriteLine($"{collision.First} touched {collision.Second}"));
world.Events.Subscribe<ButtonPressedEvent>((_, pressed) => Console.WriteLine($"button {pressed.Button} was pressed"));

world.Events.Raise(new EntityDestroyedEvent(entity));   // queued like everything else
world.Events.Dispatch();                                // or let World.Update do it around the systems of the step
```

`CollisionEvent` comes from the collision system for every overlapping pair of the step, `ButtonPressedEvent` from the UI
in the frame the pointer goes down on a button, and the world itself announces `EntityCreatedEvent`,
`EntityDestroyedEvent`, `ComponentAddedEvent<T>` (a component that appears, not one that is written over) and
`ComponentRemovedEvent<T>`. `EntitySystem` is a base for systems that work this way: it declares its subscriptions in
`Subscribe` once, before its first update, and works per step in `OnUpdate`; `Enabled` stops a system from updating while
leaving its subscriptions in place, which is what a pause menu wants. The bus belongs to the world, so a new world needs
its subscriptions again.

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

An entity that a spawn made is written as the prototype it came from plus only the components that differ from what that
prototype declares, so a map of a hundred units of one kind holds a hundred references rather than a hundred copies:

```csharp
Entity goblin = spawner.Spawn(world, "Goblin", new Vector2(320f, 240f));
world.GetRef<TransformComponent>(goblin).Scale = new Vector2(2f, 2f);

// the scene keeps { "Prototype": "Goblin", "Components": { "Transform": ... } } and nothing of the collider
string map = scenes.Save(world);
```

`World.PrototypeOf` reports what an entity was made from, and `World.AssignPrototype` says the same for an entity that a
game built by hand. Reading such a scene needs the content of the game, because the entities are made again from it: a
scene that names a prototype while no content is loaded is refused rather than loaded empty.

A sprite names its image by path, and the path is what a scene keeps:

```csharp
world.Set(entity, new SpriteComponent
{
    TexturePath = "Textures/Tiles/tiles.bmp",
    Size = new Vector2(64f, 64f),
    Color = Color.White,
});
```

The renderer resolves the path the first time the sprite is drawn, so a loaded scene draws without a
game putting device handles back by hand. An image that is not there is drawn as the placeholder of
`ITextureService` — a built-in checkerboard that spells out ERROR — counted by `MissingCount` and
reported once per path, which turns a typo in a path into something a person sees rather than a frame
that fails.

A scene keeps the identifier of every entity, so a component that refers to another entity survives the save: write the
reference as `EntityRef`, which a world makes with `World.Reference` and reads back with `World.Resolve`.

```csharp
world.Set(unit, new TargetComponent { Target = world.Reference(enemy) });

foreach (Entity owner in world.Enumerate<TargetComponent>())
{
    Entity target = world.Resolve(world.Get<TargetComponent>(owner).Target);

    if (world.IsAlive(target))
    {
        // the reference still names an entity that exists
    }
}
```

`World.SceneIdOf` reports the identifier an entity carries and `World.TryEntityOf` maps one back to an entity. A load
writes the identifiers of the scene into the world, and an entity the world already holds that uses one of them is given
a fresh identifier, because the identifiers of the scene win. A scene that was written in a version this build does not
read is refused, and so is one that names the same identifier twice. `Save` writes `SceneData.Version`, and text without
it counts as the current version.

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

## Load content and spawn

```csharp
PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();
SpawnService spawner = provider.GetRequiredService<SpawnService>();

prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);
prototypes.Load(assets, "Prototypes");

Entity goblin = spawner.Spawn(world, "Goblin", new Vector2(320f, 240f));
```

`AddAgeContent()` registers both. A spawn is where the content of a game becomes
entities: the manager reads every `*.yml` under `Resources/Prototypes` before it
resolves any of them, so a document that names a component nothing registered, a
parent that no file declares, or a field the component does not have is refused
at startup with the file and the line of the mistake. A document whose `type` is
`entity` declares what a thing is made of, and a spawn creates an entity and
attaches exactly those components, with the values the document declares — a map
of a hundred units of one prototype is a hundred calls rather than a hundred
copies. Where a spawn puts the entity is the one thing it takes from the caller,
and a prototype that carries no transform is placed nowhere.

Content lives under `Resources`, and that folder is a catalogue: a path is `<Section>/<Subsection>/<file>`, written the
same way in the engine, in a document and in `Resources/README.md`, which a test keeps in step with the files it lists.

## Check the content of a build

```bash
dotnet run --project Age.Content.Lint -- Resources Prototypes
```

`Age.Content.Lint` reads the content of a game the way a build does and exits with a non-zero code
when anything is wrong, so a mistake in a document fails a build rather than a fight: a document that
is not a prototype, a component that nothing registered, values that its contract cannot read, a field
that the format of the component does not carry — which is how a field that belongs to a run rather
than to content, such as the handle of a texture, is refused instead of being dropped — a parent that
no file declares, a kind that nothing reads, a circle of inheritance, and a path that a
`[ResourcePath]` field names, which is checked against the files of the build. The sheets of a build are read
too: a sheet says which version of the format it is written in and where its art comes from with `license` and
`copyright`, and the grid it declares is checked against the size of the image beside it.

The tool knows the kinds and the components the engine ships. A game with kinds of its own reads the
same check from its own host:

```csharp
var linter = new ContentLinter(prototypes, provider.GetRequiredService<ComponentRegistry>(), assets, provider.GetRequiredService<IImageLoader>());
LintReport report = linter.Lint("Prototypes");
LintReport sheets = linter.LintSheets("Textures");   // the grid of a sheet against the image beside it, its version and its licence

foreach (LintProblem problem in report.Problems.Concat(sheets.Problems))
{
    Console.Error.WriteLine(problem);   // Prototypes/Entities/goblin.yml(7): ...
}

return report.IsClean && sheets.IsClean ? 0 : 1;
```

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

## Draw an animated character

The frames of a character live in an image and a document beside it, which says where each state lies on the grid:

```yaml
version: 1
license: MIT
copyright: AGE, drawn for this repository
image: Textures/Entities/goblin.tga
cell:
  X: 16
  Y: 16
columns: 4
rows: 2
states:
  idle:
    row: 0
    frames: 2
    delay: 0.4
  attack:
    row: 1
    frames: 3
    delays:
      - 0.18
      - 0.08
      - 0.15
    loop: false
```

A state gives one length for every frame with `delay`, or the length of each frame with `delays` — a wind-up, a strike
and a recovery are not the same length. A sprite names the document, the state and the frame; an animation plays a state
on the time of the simulation:

```csharp
world.Set(goblin, new SpriteComponent { SheetPath = "Textures/Entities/goblin.yml", State = "idle", Color = Color.White });
world.Set(goblin, new SpriteAnimationComponent { State = "attack" });
```

`SpriteAnimationSystem` runs in the fixed step next to the timers, writes the frame that is on screen into the
`SpriteComponent` of the same entity, and raises `SpriteAnimationFinishedEvent` once when a state that does not loop
reaches its last frame:

```csharp
world.Events.Subscribe<SpriteAnimationFinishedEvent>((entity, @event) =>
{
    ref SpriteAnimationComponent animation = ref world.GetRef<SpriteAnimationComponent>(entity);
    animation.State = "idle";
    animation.Paused = false;
});
```

No coordinate of an image is written in a game: the region of a frame is arithmetic over the grid that the document
declares. A part of a character that is placed or animated apart from the rest is an entity of its own with a higher `ZOrder`
— the layers of one sprite are what a part is when it is one picture drawn in one place — and a direction is a state of its
own. A document, a state or an image that is not there is drawn as the placeholder of the texture service and reported once — the
overlay of a build names the sheets and the states behind those placeholders — and `Age.Content.Lint` reads every document
under the textures of a build: it checks the paths a prototype names against the files a build ships, the grid of a sheet
against the image it names, that every sheet says which licence its art comes with and who it belongs to, and that every
state a prototype names — on the sprite or on its animation — is one the sheet it names declares.

## Draw text

```csharp
IFontService fonts = provider.GetRequiredService<IFontService>();
FontHandle font = fonts.Load("Fonts/Cousine-Regular.ttf", 24f);

Vector2 size = fonts.Measure(font, "Hello AGE");
fonts.Draw(font, "Hello AGE", new Vector2(32f, 32f), Color.White);
```

`IFontService` reads the file through `IAssetLoader`, bakes its glyphs into one atlas with `StbTrueTypeSharp` and
uploads that atlas as a texture, so text is tinted and drawn like any other sprite. A font is cached by its path, its
height and the range of characters it was baked for, so the same file at the same size returns the same atlas. The bake
covers the printable ASCII range by default, and a caller names the range it needs with the first and the last character
of it — the space to the end of the Cyrillic block is what a language with another script needs — while a character
outside the range of an atlas is drawn as a space, which keeps the rest of the line where it was. A wide range costs
little, because a character the font has no glyph for takes no room in the atlas. `Measure` reports what a line
advances and how tall it is, which is what places the text of a menu. Draw between `BeginFrame` and `EndFrame`, from the
render callback of the loop or from a render pass. The sample ships `Resources/Fonts/Cousine-Regular.ttf` under the SIL
Open Font License 1.1 for exactly this call, and its asset root is the shared `Resources` folder, so the paths it
passes are relative to that folder.

The built-in 8x8 bitmap font is still there for a game that ships no font: a text that names no font, or whose font cannot be
baked, is drawn with it, and it is measured through the same seam as any other font, so it can be wrapped and aligned like
one. It covers the printable ASCII range only, which is why the engine ships a font of its own and draws every text that names
none with it: `TextDefaults.Fonts` is that stack, `TextDefaults.FontPath` names the file (the SIL Open Font License, as the
other fonts of the repository), and the console of the developer overlay, the numbers of a frame and any line a game holds
without an entity are drawn with it through `TextRenderer.Draw(string, …)`, advanced by `TextRenderer.LineHeight`.

## Play in another language

```csharp
ILocaleService locale = provider.GetRequiredService<ILocaleService>();
locale.Language = "ru";

string name = locale.Get("ent-Goblin");                          // гоблин
string items = locale.Get("ui-entities", ("count", 3));          // 3 сущности

// A line of the world names a key rather than a string, so it says what the language of the game says, and the stack of
// fonts is what covers the script of that language.
Entity label = world.CreateEntity();
world.Set(label, new TransformComponent { Position = new Vector2(300f, 180f) });
world.Set(label, new TextComponent
{
    Key = "ent-Goblin",
    Style = new TextStyle
    {
        Fonts = [new FontStyle { Path = "Fonts/Cousine-Regular.ttf", PixelHeight = 24f, FirstCharacter = ' ', LastCharacter = '\u04FF' }],
        Color = Color.White,
    },
    ZOrder = 10,
});
```

`ILocaleService` answers a game with the string of a key in the language it plays in, and that language starts as the one the
system is set to, when the game ships it, so a player who never chose one reads their own rather than English:
`SystemLanguage` says which one that is and `Language` is what changes it. The strings are content:
`Resources/Locale/<language>/…` holds documents of keys in the same subset of YAML as the rest of the content, so a new
language is a folder rather than a change in code. A key that the language does not hold falls back to the base language
(`en`) key by key, so a translation that is not finished shows English rather than keys, and a key that neither holds is
answered with the key itself, counted in `Missing` and written once in the log rather than taking a frame down. A text
that writes a count picks the form its language selects — English has `one` and `other`, Russian has `one`, `few`, `many`
and `other` — and a prototype names its strings with the fields `name` and `desc`, which hold keys such as `ent-Goblin`
rather than texts, so a spawned goblin is named without a document writing the name again.

`Age.Content.Lint` reads every language of a build and refuses a key that two documents write, a reference that its
language does not answer, a translation that holds a key the base language does not, and a name or a description that a
prototype points at and no string answers, so a language is checked before a build ships rather than by a player.

A `TextComponent` says what a line of the world or a label of the interface says, and where the line stands is what tells the
two apart: an entity with a `TransformComponent` has its line at the transform, and one with a `RectTransformComponent` has
its label in that rectangle, which is also the box the text is laid out into. `Key` names the string of the game with
`Count` written into it as the count of its plural form, or `Text` holds a string of the game's own. `Style` says which
fonts may draw the text and how its lines fit the box: wrapping at a space or anywhere, alignment across and down the box, an
ellipsis where the text does not fit, and a stack of fonts, of which the first that covers a character draws it, which is
what mixes the letters of two scripts in one line. What the renderer resolved — `MeasuredSize` and `Font` — is written back
for a game to read, which is what a panel that follows its title or a button as wide as its word needs. The pass of the text
runs between the pass of the world and the pass of the UI:

```csharp
renderPipeline.Add(renderSystem);
renderPipeline.Add(provider.GetRequiredService<TextRenderSystem>());
renderPipeline.Add(uiRenderSystem);
```

## Lay out the interface at any resolution

```csharp
Entity canvas = world.CreateEntity();
world.Set(canvas, new CanvasComponent
{
    IsRoot = true,
    DesignSize = new Vector2(1280f, 720f),          // what the interface is authored against
    ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
    Match = 0.5f,                                    // zero matches the width, one the height
});

// A panel that keeps its margin from the bottom-right corner of the canvas, whatever the window is.
world.Set(panel, new RectTransformComponent
{
    Anchored = true,
    AnchorMin = new Vector2(1f, 1f),
    AnchorMax = new Vector2(1f, 1f),
    Pivot = new Vector2(1f, 1f),
    AnchoredPosition = new Vector2(-24f, -24f),
    SizeDelta = new Vector2(200f, 50f),
    Visible = true,
});

// The layout runs before the pointer test of the interface and before the pass that draws it.
pipeline.AddFrame(provider.GetRequiredService<UILayoutSystem>());
pipeline.AddFrame(provider.GetRequiredService<UIUpdateSystem>());
```

A canvas scales the interface to the window: the layout divides the size of the window by the scale of the canvas, and
writes the rectangle of every anchored element of the world back into `RectTransformComponent.Position` and `Size` in pixels
of the window, on every frame. An element that sets `Anchored` is placed by its anchors: `AnchorMin` and `AnchorMax` name the
room of the canvas it is measured in — zero is the left or top edge of it and one the right or bottom edge — `Pivot` says
which point of the element sits on that room, `AnchoredPosition` is the distance from it in design units, and `SizeDelta` is
the size of the element, or the amount by which a stretched element is larger or smaller than the room between the anchors.
The element that covers the whole canvas is one with the anchors of the two opposite corners and no size, and the element
that keeps a margin from the bottom-right corner is one anchored to that corner with the pivot of the same corner and a
negative anchored position. An element that leaves `Anchored` alone stands where `Position` and `Size` put it, in pixels,
which is what content written for one resolution keeps doing. `Match` is what the scale follows: zero matches the width of
the window, one matches the height, and a value between the two blends the ratios.

The world has the same choice, in one call: `Camera2D.Fit` builds a camera that shows a design area of the world in a window
of any size, so a game that lays its map out for 1280 by 720 draws the same view on a display of 3840 by 2160 and in a
window of another shape, with `CameraFit.Contain` keeping the whole area in view and `CameraFit.Cover` filling the window and
cropping the edges around the middle of the area, so what a game places at that middle stays at the middle of the window. A
camera that leaves the zoom at one is the other way round and is the default of the engine: one unit of the world per pixel of
the window, so a larger window shows more of the world.

## Read the keyboard and the pointer

```csharp
IInputService input = provider.GetRequiredService<IInputService>();

if (input.IsKeyPressed(Key.Escape))
{
    // Escape went down on this frame, and not on the frames it is held on.
}

Vector2 pointer = input.MousePosition;
bool clicking = input.IsMouseButtonDown(MouseButton.Left);
```

A key is a `Key` and a button is a `MouseButton`, and both are the types of the device the window reports through, so any key of a
keyboard can be asked about: `Key.W`, `Key.Escape`, `Key.F1`, `Key.ControlLeft`. Reaching for a key that the engine has never heard
of is not a change in the engine, and `Key` names the whole keyboard rather than a list of the keys the engine happened to pick.

The service describes one frame. The "down" queries report what is held right now, and the "pressed" queries report a transition
that happened since the previous frame, so a key that stays held is pressed once. The frame boundary is opened by the loop, before
the systems run, which is why a key that goes down and up inside one frame is still reported as pressed for it.

```csharp
services.AddAgeInput();       // the service that is used when no window is open: no key, no character
services.AddAgeSilkInput();   // the keyboard and the pointer of the window
```

## Draw with a shader

```csharp
IShaderService shaders = provider.GetRequiredService<IShaderService>();
ShaderHandle displacement = shaders.Load("Shaders/displacement.frag");

renderer.UseShader(displacement);
renderer.SetUniform("uDisplacementSize", 4f);
renderer.SetSampler("uDisplacement", textures.Resolve("Textures/Effects/height.png"), 2);
renderer.DrawSprite(tile, position, size, Color.White);
renderer.ResetShader();
```

A shader is content: the fragment stage lives under `Resources/Shaders`, and the engine draws it with a vertex stage of its own
that places the quad, so a shader that displaces an image is a few lines of the OpenGL Shading Language:

```glsl
uniform sampler2D uDisplacement;
uniform float uDisplacementSize;
uniform vec4 uDisplacementUv;

void main()
{
    vec4 height = texture(uDisplacement, mix(uDisplacementUv.xy, uDisplacementUv.zw, UV));
    vec2 value = (height.xy - vec2(128.0 / 255.0)) / (1.0 - 128.0 / 255.0);

    COLOR = sampleTexture(UV + value * TEXTURE_PIXEL_SIZE * uDisplacementSize * vec2(1.0, -1.0));
    COLOR.a *= height.a;
}
```

`UV`, `COLOR`, `TEXTURE`, `TEXTURE_PIXEL_SIZE` and `TIME` are what the header of the engine gives to every shader, and a game
that writes a vertex stage of its own writes it under the same header and takes the placement of the quad over. A shader is
compiled once for a pair of stages, and the quads that were collected before a shader, a uniform or a sampler changes are drawn
with the state they were collected under, because one draw call samples one program, one texture and one set of uniforms: set
what a shader needs, then draw what belongs to it.

A shader that post-processes a frame reads the surface that it is drawn into instead: the header names it `SCREEN_TEXTURE`, its
size in pixels `SCREEN_SIZE` and a reader of it `sampleScreen`, and the engine copies that surface into a texture before the
first quad of such a program is drawn, so the shader draws a picture of the frame rather than reading what it is writing. The
copy is read from the bottom row of the surface upwards, which is why `sampleScreen` and `SCREEN_UV` flip the vertical axis: the
coordinate of a quad starts at its top-left corner.

```glsl
uniform float uVignetteStrength;
uniform vec3 uVignetteColour;

void main()
{
    float distance = length(UV - vec2(0.5));
    float falloff = smoothstep(0.25, 0.75, distance) * uVignetteStrength;

    COLOR = vec4(mix(sampleScreen(UV).rgb, uVignetteColour, falloff), 1.0);
}
```

A pass that draws a quad over the frame with a shader like that darkens everything that was drawn before it — the world, the
text and the interface — and a pass that runs after it, such as the overlay of the developer, stays above it. A frame of several
layers is built from render targets instead: `CreateRenderTarget` makes a surface with a texture of its own, `BeginRenderTarget`
points the draws of a pass at it, `EndRenderTarget` points them at the window again, and what was drawn into a target is a
texture that a shader binds as a sampler of its own — the third unit and up, because the first two belong to the engine — or
draws as a quad. While a target is bound, `ViewportSize` answers with the size of it, so a pass, a camera and the layout of the
interface measure the target rather than the window.

## Draw a sprite of layers

A sprite of one image is what most things of a world are. A thing that is a picture drawn over another picture — a body, the
clothes over it and the glow over both — is one sprite of layers instead, and every layer brings the image and the stage that
draws it:

```yaml
- type: entity
  id: Beacon
  components:
    - type: Sprite
      Color:
        R: 255
        G: 255
        B: 255
        A: 255
      Size:
        X: 40
        Y: 40
      Layers:
        - Name: base
          Image: Textures/Tiles/tiles.bmp
        - Name: pulse
          Image: Textures/Tiles/tiles.bmp
          Material:
            Id: Pulse
```

The layers are drawn in the order the document writes them, so the first one is at the bottom, and every layer is drawn at the
position, the size and the colour of the sprite: a sprite of layers writes `Size`, because the engine does not read the size of an
image back from the device, and it writes `Color`, because a sprite that names no colour draws its layers in no colour at all. A
layer that names no `Material` is drawn with the program of the engine, which is what an unshaded layer is.

```csharp
world.Set(beacon, new SpriteComponent
{
    Size = new Vector2(40f, 40f),
    Color = Color.White,
    Layers =
    [
        new SpriteLayer { Name = "base", Image = "Textures/Tiles/tiles.bmp" },
        new SpriteLayer { Name = "pulse", Image = "Textures/Tiles/tiles.bmp", Material = new Material { Id = "Pulse" } },
    ],
});
```

The program of a material is compiled on the first frame that draws it and kept while the renderer stays attached to the window it
was compiled against, and the pass switches to another program only when a layer names one that the layer before it did not: one
draw call samples one program, so the layers of a sprite that share a material are drawn in one call and the layers that alternate
are drawn in several. A stage that cannot be loaded is reported once in the log and its layer is drawn without it, which is what
an image that is not there does as well. A part of a character that is placed or animated apart from the rest is still an entity
of its own with a higher `ZOrder`; layers are what a part is when it is one picture drawn in one place.

## Draw a sprite with a material

A layer that names the path of a stage writes the same program once per sprite, and the values of its uniforms are then the code
of a game. A material is the same program as content instead: one document holds the stage and the values its uniforms start with,
and every layer that names it is drawn by it, so a hundred sprites that shine the same way are a hundred names of one material and
a change of the glow is a change in one file.

```yaml
- type: material
  id: Pulse
  fragment: Shaders/pulse.frag
  uniforms:
    Speed:
      float: 4.0
```

A value is written as the kind of the uniform and the numbers behind it, and the kind is spelled the way GLSL spells it, so a
value that a stage reads as a `float` and one it reads as an `int` are told apart. A value of one number is the number on the line
of its kind; a vector and a colour are a block sequence, one number to a line, because a list written on one line is not part of
the YAML the content of the engine is read with:

```yaml
  uniforms:
    Speed:
      float: 4.0
    Steps:
      int: 8
    Direction:
      vec2:
        - 1.0
        - 0.0
    Tint:
      color:
        - 255
        - 220
        - 120
        - 255
```

`float` and `int` hold one number; `vec2` through `vec4` hold two to four of them; and `color` holds four numbers written the way
every other colour of the content is written, in bytes, and read as the four channels between zero and one that a stage draws
with. The stage of a material draws with the program of the engine unless the document names a vertex stage as well, and a value
that its kind cannot hold is refused where the content is read: a material is data of a build, so a mistake in it is a message at
the start of a game rather than a frame that quietly draws something else.

The values are written as a block, with the kind on its own line and the numbers of a vector indented under it, because neither
the flow style — `Speed: { float: 4.0 }` — nor a list of one line — `vec2: 1.0, 0.0` — is part of the subset of YAML that the
content of the engine is read with.

A value of a layer is written the same way, so the four numbers of a colour that overrides the one of a material are a block
sequence as well:

```yaml
          Material:
            Id: Pulse
            Uniforms:
              Tint:
                color:
                  - 255
                  - 220
                  - 120
                  - 255
```

A layer that names a material and writes a value of its own is drawn with that value instead of the one of the material, so two
beacons that share `Pulse` differ by the speed of their own pulse:

```yaml
      Layers:
        - Name: slow
          Material:
            Id: Pulse
            Uniforms:
              Speed:
                float: 1.0
        - Name: fast
          Material:
            Id: Pulse
            Uniforms:
              Speed:
                float: 8.0
```

```csharp
IMaterialService materials = provider.GetRequiredService<IMaterialService>();

foreach (MaterialPrototype material in prototypes.Enumerate<MaterialPrototype>())
{
    materials.Register(material.Id, material);
}

materials.Build();
```

A game reads its own values with the same document: `Read<T>` reads the uniforms of a material into the struct that holds them,
which is what a pass of a game sends each frame.

```csharp
struct PulseUniforms
{
    public float Speed;
}

PulseUniforms uniforms = prototypes.Get<MaterialPrototype>("Pulse").Read<PulseUniforms>(components);

materials.SetShader(renderer, materials.Shader(new Material { Id = "Pulse" }), [new IMaterialService.UniformValue("Speed", IMaterialService.UniformKind.Float, [uniforms.Speed * 2f])]);
```

## Load and play a sound

```csharp
ISoundService sounds = provider.GetRequiredService<ISoundService>();
SoundHandle click = sounds.Load("Audio/Effects/click.wav");

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

gameLoop.Run(
    update: step => world.Update(step, pipeline),
    render: time =>
    {
        if (splash.Draw(renderer, time))
        {
            return;
        }

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

RenderPipeline renderPipeline = provider.GetRequiredService<RenderPipeline>();
renderPipeline.Add(provider.GetRequiredService<RenderSystem>());
renderPipeline.Add(provider.GetRequiredService<UIRenderSystem>());

var camera = new Camera2D { Position = Vector2.Zero, Zoom = 1f, ViewportSize = new Vector2(1280, 720) };

provider.GetRequiredService<IGameLoop>().Run(
    update: step => world.Update(step, pipeline),
    render: time =>
    {
        world.UpdateFrame(time, pipeline);
        camera.ViewportSize = renderer.ViewportSize;
        renderPipeline.Render(world, camera);
    });
```

`World` is never registered in the container; the caller owns its lifetime.

The render pipeline runs its passes in the order they were added, so the UI lands on top of the world. Add a pass of
your own after those two, for post-processing or an overlay, and it draws last.

The camera takes the size of the window before the passes run, so the culling of the world pass and the projection of
the renderer always agree, including after the window was resized.

Dispose the renderer before the window is closed, and unload the textures and the splash logo before that: their device
objects live in the OpenGL context of the window. The container disposes the services when it goes out of scope, which
is after the window is gone, so a game that wants a clean shutdown releases them itself:

```csharp
splash.Dispose();
textures.UnloadAll();
renderer.Dispose();
windowService.Close();
```

The loop accumulates the time each frame took in its `FixedTimestep` and calls the update callback a whole number of
times with one sixtieth of a second, so the simulation advances by the same amount at any frame rate, and the render
callback runs once per frame after those steps. A frame that took longer than a quarter of a second counts as if it
took that long, so a breakpoint or a window drag does not produce a burst of steps. Register a `FixedTimestep` of your
own before the loop is resolved to change the step, and read `FixedTimestep.Alpha` to draw a position between two steps.
Call `Run(tick)` instead to receive the time of every frame directly.

## Pause the game and follow the display

```csharp
FixedTimestep clock = provider.GetRequiredService<FixedTimestep>();

pipeline.AddFrame(provider.GetRequiredService<UIUpdateSystem>());  // once per frame: the interface

clock.Paused = true;     // the steps stop, the frames keep coming
clock.TimeScale = 0.5d;  // slow motion, without touching FixedTimestep.Step
```

`FixedTimestep` is the clock of the game as well as the accumulator of the frame time. `Paused` stops the simulation
without stopping the frames: the render callback of the loop still runs, and so do the frame systems of the pipeline, so
a pause menu still reacts to the pointer and still animates. The time of a paused frame is discarded rather than
accumulated, and a pause that a system sets from inside a step stops the remaining steps of that frame, so resuming does
not replay the time that passed while the game stood still; `Tick` and `Elapsed` describe the simulation and stop with it.

A system that has to follow the display rather than the simulation implements `IFrameSystem` instead of `ISystem`, is
registered with `SystemPipeline.AddFrame`, and is called by `World.UpdateFrame` from the render callback. A long frame
runs several steps and exactly one frame, which is what a camera that smooths, a menu that fades or a debug overlay
needs. `FixedTimestep.Alpha` still reports the fraction of a step that is left over, for a world that interpolates.
