# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- A sprite sheet is data: the document next to an image declares the grid (`cell`, `columns`, `rows`) and the states over it
  (the row a state lies on, the frames at the start of it, the length of a frame and whether it repeats), and
  `SpriteComponent.SheetPath` together with `State` and `Frame` picks one frame of it, so a game never writes a normalized
  coordinate and a region is arithmetic over the declared grid. `SpriteSheetReader` reads that document in the same subset
  of YAML as the rest of the content and refuses a state that does not fit the grid with the file and the line.
  `ISpriteSheetService` answers a renderer with the texture and the region of a frame — a document, a state or an image that
  is not there is the placeholder and one line in the log rather than an exception in the middle of a frame — and
  `SpriteAnimationSystem` plays a state on the time of the simulation, writes the state that plays and its frame into the
  sprite, and raises `SpriteAnimationFinishedEvent` once for a state that does not repeat. A layer of a character is an
  entity of its own,
  because the engine draws in `ZOrder`, and a direction is a state of its own. The repository now ships an animated goblin:
  `Textures/Entities/goblin.tga` with the document beside it, which the prototype of the goblin names, so the content of the
  engine animates without one line of code about frames.
- The `Resources` folder is a catalogue rather than a pile: `Resources/README.md` says where a file goes
  (`<Section>/<Subsection>/<file>`, the same path in the engine, in a document of content and in the table), lists every
  file the folder holds and who uses it, and names the sections that arrive with a later step (`Locale` with F2, `Maps`,
  `Ui` with B6, `Shaders`), so nothing is committed as an empty promise. Two tests walk the folder and check both
  directions: a file that no line of the catalogue names fails the build, and a path that a line names but the folder does
  not hold fails it too.
- The format of a component is what a document may write: the registrations that the source generator writes carry the names
  of the fields of a component, and reading a document refuses a field that is not one of them. That closes the case the
  contract used to ignore in silence, where a document that named the handle of a texture rather than the path of an image
  was read without the field: a prototype refuses it as content that is wrong, and a scene refuses it as a file that cannot
  be read. A registration written by hand passes no names and leaves the decision to its own contract, and the content
  linter keeps its check for that case.
- `Age.Content.Lint`, a tool that reads the content of a game the way a build does and exits with a non-zero code when
  anything is wrong, so a broken prototype fails a build rather than a fight. It wraps the reader — which now names the
  file of a document in every refusal, because the YAML reader knows the line and not the file it was reading — and adds
  the check a reader cannot make: a path that a `[ResourcePath]` field names is a well-formed word whether or not a file
  stands behind it, so a build that ships a typo fails rather than drawing the placeholder.
  `IPrototypeManager.Load` and `IPrototypeManager.Prototypes` are the content side of it: a tool reads
  the data of every prototype without knowing what a game reads them as. CI runs the tool over the content the engine
  ships, and a game with kinds of its own reads `ContentLinter` from its own host.
- A sprite names its image by path: `SpriteComponent.TexturePath` is what a prototype or a scene writes, and the texture
  service resolves it the first time the sprite is drawn, so a scene that was loaded draws without a game putting device
  handles back by hand — the last piece of code that a game had to write around a save is gone from the sample. The handle
  belongs to the device and is no longer part of a scene (`SpriteComponent.Texture` is not written), which also means a
  document that writes a number for it is refused rather than silently saved.
- An image that content names and a build does not ship is drawn as a placeholder instead of taking the frame down:
  `ITextureService.Error` is a built-in checkerboard that spells out ERROR and needs no file, `Resolve` answers with it for
  a path that cannot be loaded, reports that path once in the log and counts it (`MissingCount`, `Missing`), and the
  developer overlay names the images that are missing next to the numbers of the frame. The sample puts one on screen from
  its console with `broken`.
- A scene stores an entity that came from a prototype as a reference to it plus only the components that differ from what
  the prototype declares: `SpawnService` records the prototype when it makes an entity, `World.PrototypeOf` reports it, and
  `World.AssignPrototype` is how a game says that an entity it built by hand belongs to a kind. A map of a hundred units
  of one kind is therefore a hundred references rather than a hundred copies of the same components, and one of them being
  bigger than its kind is written as the difference that it is. Reading such a scene makes the entities again through the
  content (`IPrototypeSource`, which the serializer takes from the container), so a scene that names a prototype while no
  content is loaded is refused rather than loaded empty. A component that matches its prototype in every field is left
  out, which is measured through the contract of the component: the fields that a document leaves out count as the values
  the component starts with, whatever the order of the fields. An entity that lost a component of its prototype is written
  in full, because a scene has no way to say that. The format is version two now, and a scene of version one reads as it
  did.
- `EntityPrototype` and `SpawnService`: content that becomes entities, which is what makes a map, an enemy or an item a
  document rather than a class. A document whose `type` is `entity` declares what a thing is made of, and
  `Spawn(world, "Goblin", position)` creates an entity and attaches exactly those components, with the values the
  document declares, read by the same contract that reads a scene — so what a document of content says and what a saved
  map says reach an entity the same way. A spawn places what carries a transform, `TrySpawn` reports a prototype that the
  content does not hold, a component that nothing registered is refused with the file and the line that named it, and a
  map of a hundred units of one prototype is a hundred calls rather than a hundred copies, and a spawn that fails leaves
  the world as it was, because every component of a prototype is read before anything of it reaches an entity.
  `AddAgeContent` registers the service over the manager and the component registry, and the sample puts a goblin of the
  content in the world from its console.
- `IAssetLoader.Enumerate`, which returns the files of a folder in it and below it, ordered by ordinal and written with a
  forward slash whatever the platform uses, so a folder of content is read the same way on every machine, and a link is
  not followed, so walking the content of a game never leaves the game root; a folder that cannot be walked is refused by
  name. `PrototypeManager.Load(IAssetLoader, folder)` reads every `*.yml` and `*.yaml` of a folder before it resolves any
  of them, because a prototype may inherit from one that another file declares, leaves files
  that are not documents alone, and treats a folder that is not there as content that holds nothing. `AddAgeContent`
  registers the manager over the component registry of the container. `Resources/Prototypes` now ships the first content
  of the engine: a `CreatureBase` with the goblin that inherits it, and an `ItemBase` with the sword that inherits it,
  which the tests load, inherit and validate, and which the sample reads and counts at startup.
- `ProtoId<T>`, `IPrototype`, `Prototype` and `PrototypeManager`: the content of a game as data. A document declares a
  prototype by an identifier, names the components it carries and the values they start with, and can inherit from
  another prototype with `parent`; the manager reads every document first and then builds, which is the point where the
  mistakes of a whole content are reported at once: an identifier that two files declare, a component that nothing
  registered, values that the contract of a component cannot read, a field the component does not have, a parent that no
  document declares, a kind that nothing reads, and prototypes that inherit from each other in a circle. Every refusal
  names the file and the line, so a broken document is a message at the start of a game rather than a surprise in the
  middle of a fight. `ProtoId<T>` is how a component refers to a prototype, and a scene writes it as a plain word, so a
  map refers to the data of an enemy rather than carrying a copy of it.
- `YamlJson`, which hands the values of a document to the contract a scene uses: a prototype and a scene describe one
  component with one reader, and the registry gained `TryDeserialize` and `Has` for exactly that, so a component that a
  prototype names is read by the code that reads a scene.
- `Age.Content`, the assembly that holds the content of a game, with its `YamlReader` first: the subset of YAML that the
  content of the engine is written in — scalars, lists, dictionaries and the nesting of them, with comments, empty lines
  and quotes — read into a tree where every value remembers the line it came from. The subset is fixed on purpose
  (ADR-8): a document that asks for anchors, aliases, tags, flow style or block scalars is refused with the line and the
  column of the mistake, and so is a name that appears twice, a line that does not line up with its block, and a value
  that is both a word and a block.
- `Age.SourceGen`, a source generator that writes the `IComponentRegistrations` of an assembly from its `[Component]`
  attributes, so a component is declared once and its registration follows. The hand-written registration classes of
  `Age.Core`, `Age.UI`, `Age.Physics` and `Age.Rendering` are gone. A type that is named as a component but is not a
  struct, or does not implement `IComponent`, is an error of the build (AGE0001, AGE0002) rather than a silent omission.
  The serialization context cannot be written the same way, and is not: the source generator of `System.Text.Json` never
  sees what another generator adds to a compilation, so the `[JsonSerializable]` entry stays with the context of the
  assembly. A missing one is now a compile error of the generated registration, which is louder than a component that
  turns out to be unsavable after a restart.
- `[Component("Transform")]`: the one declaration that makes a component type part of a scene, and the name a scene file
  uses for it. A test of the engine walks every public component of `Age.Core`, `Age.Physics`, `Age.UI` and
  `Age.Rendering` and fails when one of them has no name, is not registered in the `ComponentRegistry`, or shares a name
  with another component, so a forgotten registration is a failing test rather than a component that turns out to be
  unsavable after a restart.
- `ITextInputService`: the characters that were typed during the current frame, which is what a console or a text field
  reads. It is a separate interface from `IInputService` because it describes text rather than keys, and a service with no
  keyboard reports none rather than being absent. `NullInputService` answers it from its `Typed` property, so a test drives
  what a console reads, and `SilkInputService` collects the characters of the window. `AddAgeInput` registers one instance
  behind both interfaces, and `AddAgeSilkInput` registers the service that reads the window behind both as well.
- `DevOverlay`, the developer overlay of a game: the numbers of the frame (frames per second, entities, the tick of the
  clock, and what every system spent, step and frame) and the console of the engine, drawn with the built-in bitmap font
  over everything else because it is a render pass like any other. Its console key, Tab by default, opens and closes the
  console and stands the simulation still while it is open, putting the clock back the way it was when it closes; the
  characters that were typed reach it, Enter runs the line, backspace removes a character, and the up and down keys walk
  its history. Its second key, F1 by default, shows and hides the numbers. `Key.Backspace` and `Key.F1` are part of the
  input of the engine from now on, and the sample registers a command, a setting and the overlay itself.
- `IConsoleService` and `ConsoleService`: the commands a developer registers, the lines that were entered with their
  history, and the lines that were written, which the engine draws and a game drives from a key of its own. Typing,
  backspace, submitting and walking the history are the service, so a command is testable without a window, and it is
  safe to write to from another thread. `help` and `clear` are always there, and a name is matched without regard to case.
- `CVarService`: named settings with a type, a description and a default, which code, a configuration source and the
  console all read and write with the invariant culture. Give the service a console and every setting becomes a command
  of its own, next to a `cvars` listing, so a parameter can be looked at and changed without rebuilding the game.
- `SystemTiming`, `SystemPipeline.StepTimings` and `SystemPipeline.FrameTimings`: what every system spent inside the last
  step and the last frame, in milliseconds, which is what the developer overlay of a game prints.
- `ConsoleLoggerProvider`, which writes log records into a console, so what the engine and a game report is visible in a
  running build. `AddAgeCore` also registers `NullLogger<T>` as the logger of every service that asks for one, so a game
  never has to wire logging and a logger is never null.
- `SceneSerializer` reports what it did through an `ILogger<SceneSerializer>`: a scene that was refused leaves the reason
  in the log before the exception reaches the caller, and one that was applied leaves the number of entities it brought.
  `NullAssetLoader` does the same for an asset that does not exist or whose path escapes the game root, and `SilkRenderer`
  for a frame that was drawn before it was attached to a window, so a failure of a load or of a frame is a line a person
  reads rather than a silence.
- `GameShutdown` and `IGameShutdownStep`: the engine releases what it holds in one call, in an order that every assembly
  declares next to the thing it releases, instead of leaving that order to the game. The splash and what it uploaded, the
  atlases and the textures of a frame, the samples of the sound device, the renderer, and the window that owns the context
  last. A game registers steps of its own for what it holds. A step that throws does not stop the steps behind it,
  because a shutdown that stopped halfway would leak everything it skipped, and the call is idempotent.
- Scenes carry the identifier of every entity (`SceneEntity.Id`) and the version of the format (`SceneData.Version`), so
  a reference between entities survives a save and a load. `World.SceneIdOf` reports the identifier an entity carries,
  `World.TryEntityOf` and `World.Resolve` map one back to an entity, and `World.Reference` makes a reference out of an
  entity. A load writes the identifiers of the scene into the world; an entity the world already holds that uses one of
  them is given a fresh identifier, because the identifiers of the scene win. A scene that names the same identifier
  twice, or one that was written in a version this build does not read, is refused before the world is touched. Text
  without a version counts as the current one, so a scene that was written by hand still reads.
- `EntityRef`, a reference to another entity that survives a save and a load, which is written as a plain number by
  `EntityRefJsonConverter`. A reference to an entity that a world does not know resolves to the default entity, not to a
  wrong one, so a scene that refers to something it does not hold cannot be mistaken for a working one.
- `SceneEntity.Prototype`, which a scene writes and reads but which nothing turns into an entity yet: it is where an
  entity stops repeating the components of its kind and starts referring to them.
- `TimerComponent` and the `TimerSystem` that advances it. A timer counts down on the time of the simulation, keeps the
  leftover of the step that ran past its end, repeats on request, and stands still while it is paused, which is how a game
  cancels one. A timer that runs out announces it through `TimerElapsedEvent` and stops instead of removing itself, so
  the entity keeps the record of what happened and the game decides what to do with it.
- `TweenComponent` and the `TweenSystem` that advances it. A tween moves a value from one number to another over a
  duration, with an easing, and announces the value it has reached through `TweenUpdatedEvent` on every step it moves,
  which is what lets the game write that value where it belongs while the component itself holds nothing but numbers: a
  delegate in a component could neither be saved nor described without reflection. `Value` and `Progress` are readable at
  any time as well, so a game that only reads the value does not have to subscribe. A tween that does not loop reports
  its end once and then stops, and a looping one starts over.
- An event that carries its own entity, such as a timer or a tween, is raised without naming an entity to the bus, so the
  first parameter of a handler is the default entity for those. Read the entity out of the event itself; the world names
  one to the handler only for the events that come without one, such as a creation or a component that was added.
- The event bus: `World.Events` delivers events to the code that subscribed to them. An event is a value, raising one
  queues it, and `EventBus.Dispatch` hands the queue out. `World.Update` and `World.UpdateFrame` dispatch it at the
  boundaries of the step, next to `World.ApplyPending`, so a handler never runs in the middle of a system that is
  changing the world, and an event that a handler raises waits for the next boundary instead of cascading inside the
  same step. `Subscribe`, `Unsubscribe` and the two `Raise` overloads are the whole surface; a subscriber receives the
  entity an event was raised for, or the default one for a broadcast.
- The world announces its own events: `EntityCreatedEvent`, `EntityDestroyedEvent`, `ComponentAddedEvent{T}` (a
  component that appears, not one that is written over) and `ComponentRemovedEvent{T}`.
- `EntitySystem`, a base for systems that react to events: it declares what it listens to in `Subscribe`, once, before
  its first update, and works per step in `OnUpdate`. `Enabled` stops a system from updating while leaving its
  subscriptions in place, which is what a pause needs.
- `CollisionEvent`, raised by `CollisionSystem` for every overlapping pair of a step, and `ButtonPressedEvent`, raised
  by `UIUpdateSystem` in the frame the pointer goes down on a button.
- Deferred operations on a world, for the code that runs while the world is being enumerated:
  `World.RequestCreate`, `RequestDestroy`, `RequestSet{T}` and `RequestRemove{T}` queue an operation, and
  `ApplyPending` applies the queue in the order it was received. `World.Update` and `World.UpdateFrame` apply it before
  and after the systems of the step, so a request made by a system or a frame system takes effect before the next one.
  An entity whose destruction was requested is not alive any more for the rest of the step, so `IsAlive` and `Has{T}`
  already report it as gone while its components stay in place until the queue is applied, and neither `Enumerate` nor
  `Enumerate{T}` visits it. Creation is queued as well: `RequestCreate` reserves the identifier right away, so the
  requests that follow it can already use it, and the entity becomes part of the world when the queue is applied. A
  write that was requested for an entity that is destroyed earlier in the same batch is dropped, while one that was
  requested before that destruction still lands: the queue decides the order. Destroying a reserved
  entity outright cancels the creation: the slot goes back to the pool, its generation advances so the identifier stays
  stale, and the queued creation for it becomes a no-op.
- The game clock: `FixedTimestep` gained `Tick`, `Paused` and `TimeScale`, so a game stops the simulation without
  stopping the frames and slows it down or speeds it up without changing the step. A paused clock discards the time of
  the frames that pass instead of accumulating it, so a game that was paused for a minute does not resume by running a
  minute of steps. A pause that a game sets from inside a step stops the remaining steps of that frame and drops what was
  left of its time, so the simulation stops where it was paused; `Tick` and `Elapsed` describe the simulation and stand
  still with it.
- `IFrameSystem`, `SystemPipeline.AddFrame` and `UpdateFrame`, and `World.UpdateFrame`, for behaviour that follows the
  display rather than the simulation: a frame system runs once per frame from the render callback of the loop, receives
  the time of that frame rather than the fixed step, and keeps running while the clock is paused. A `SystemPipeline`
  therefore holds two lists, the systems of the simulation and the frame systems.
- `FixedTimestep.TimeScale` rejects a value that is negative, not a number or infinite, because such a value would stop
  the simulation quietly.

### Changed

- Every assembly keeps its files in folders of their kind rather than in one flat folder: `Components`, `Systems`,
  `Events`, `Scenes`, `World`, `Time`, `Math`, `Graphics`, `Resources`, `Diagnostics`, `Shutdown`, `Passes`, `Overlays`,
  `Silk`, `Loaders`, `Formats`, `Devices` and `Collisions`, with the service collection extension of an assembly left at
  its root as its entry point. Nothing about the namespaces changed, so no code has to follow the move: a folder says what
  a file is, and the namespace says what an assembly offers. The tests are grouped the same way, by the part of the engine
  they cover.
- `Canvas` and `Collider` are components of a scene from now on: the canvas had no name and the physics assembly had no
  serialization context at all, so a scene could hold neither. `AddAgePhysics` registers them, next to the system it already
  registered. `Collision` is named but stays out of a scene, because a system computes its contact every step:
  `[Component("Collision", Scene = false)]` keeps what it is without letting a scene write the contact of one frame.
- **Breaking:** `ResourcePool<T>` became `ResourcePool<TKey, T>`, so a resource is registered under a key of the
  caller's own type rather than under a string, and `Add(value)` and `Add(value, key)` are separate calls. A cache that is
  keyed by more than one value, such as the fonts of `FontService`, registers the pair of the path and the height instead
  of joining them into a string with a separator of its own. `TryGetHandle` no longer rejects an empty key: a key is the
  caller's type now, and the services that load by path already reject a path that is not a path.
- **Breaking:** `SplashScreen.LogoSize` is nullable and null by default, which is what makes the logo follow the window
  instead of living with one size. Set it to draw the logo at exactly that many pixels.
- **Breaking:** `UIUpdateSystem` is an `IFrameSystem` rather than an `ISystem`, because the pointer is a state of the
  frame and an interface has to keep working while the simulation is paused. Register it with `SystemPipeline.AddFrame`
  rather than `Add`, call `World.UpdateFrame` from the render callback of the loop, and read its `UpdateFrame` where the
  old code called `Update`.
- **Breaking:** every project now lives in a folder named after it at the repository root instead of
  `src/` and `tests/`, and all build output is written to one shared `artifacts/` folder
  (`artifacts/bin/<project>/`, `artifacts/obj/<project>/`) instead of a `bin`/`obj` pair next to each
  project. Consumers must update project references and script paths: the paths are now
  `Age.Core/Age.Core.csproj`, `Age.Tests/Age.Tests.csproj`, `Age.Sample/Age.Sample.csproj` and so on.
- Every asset the repository ships moved out of the projects and into one `Resources/` folder at the root, split by
  kind: `Audio/Effects`, `Audio/Samples`, `Fonts`, `Textures/Icons`, `Textures/Logo` and `Textures/Tiles`.
  `Directory.Build.props` exposes the folder as `$(AgeResources)`, `Age.Rendering` embeds the branding images from
  it, and `Age.Sample` and `Age.Tests` copy it next to their output, so `Resources` is the asset root a game writes
  its paths against, such as `Fonts/Cousine-Regular.ttf` or `Textures/Tiles/tiles.bmp`.
- **Breaking:** `Age.Rendering` embeds its branding images under the logical names
  `Age.Rendering.Resources.Textures.Icons.*` and `Age.Rendering.Resources.Textures.Logo.*` instead of
  `Age.Rendering.Resources.*`, so a game that reads the manifest stream by name has to update the name.

### Fixed

- A prototype that declared the same component twice was read with whichever declaration came first, because nothing
  refused the duplicate: the manager refuses it now and names the component together with the line of both declarations.
- A word of a document that parses as a number only to end up as `NaN`, an infinity or a value too large for a double was
  handed to the writer of JSON, which refuses such a value and stopped the reading of a whole content with an error about
  the writer rather than about the document. Such a word goes on as the word it is, so `Rotation: NaN` is refused at load
  with the file and the line of the document that holds it.
- `World.AssignSceneId` refused to name an entity only after the entity had already given up the identifier it held, which
  left the world unable to resolve that identifier in the one case that reaches the refusal, a world that ran out of them:
  the refusal comes first now, and a creation whose identifier cannot be claimed gives its slot back and reports the
  failure instead of publishing an entity that has none.
- The generator of the component registrations takes a project property (`RootNamespace`, `AgeComponentRegistrations`,
  `AgeComponentsJsonContext`) as missing when the project declares it empty, and writes its own defaults, and it escapes a
  component name when it emits it as a literal, so a name that holds a quote cannot break the generated code.
- A command of the console that throws no longer reaches the caller: the console reports what happened in a line of its
  own and answers `false`, so a command that fails is a line a person reads rather than a game that stops.
- A scene whose identifiers are negative, or one that reaches the end of the type, is refused before the world is touched.
  The counter of the identifiers of a world saturates instead of wrapping, and it never names two entities with one number:
  a world that ran out of identifiers refuses instead of handing out one that is taken.
- `AddAgeSilkInput` registers one `SilkInputService`: the two input interfaces used to be answered by two instances, which
  opened the input context of the window twice.
- A repeating `TimerComponent` reported one run per step whatever the step consumed, so a step longer than the run left
  the timer sinking a step further behind on every step and its remaining time drifting away from zero. It now reports
  every run that the step consumed and carries the deficit into the run behind it, and a run of no length reports once per
  step and stands at zero.
- `RenderingShutdownStep` stopped at the first release that failed, which left the device objects behind it to leak: every
  release is attempted, and the first failure is thrown once all of them ran.
- A scene that turned out to be broken after part of it was checked no longer changes the world: `SceneSerializer.Load`
  records the entities that already hold an identifier of the scene and moves them aside after the whole scene was
  checked, instead of moving them while the check was still running. It also reserves every identifier of the scene before
  it creates the first entity, so an identifier this world hands out cannot land on one the scene is about to map, which
  used to make a load throw in the middle and leave the world half changed.
- `World.Enumerate<T>` built the text of its exception for every slot it walked, whether it threw or not: the message is
  built only when the storage of the component type actually changed under the enumeration.
- The built-in font drew a quad for the space of every line, whose cell in the atlas is blank: only the characters the font
  draws a mark for reach the draw call now, while the cursor still moves on for every character of the line.
- A logo the game did not size itself lived with 320 by 320 pixels, whatever the window did: the splash now computes the
  size of every frame from the shorter side of the viewport.
- `TrueTypeFontBake` allocated a coverage buffer for every glyph it rasterized, which is one allocation per glyph of every
  font: a font now allocates one buffer, as large as its largest glyph, and every rasterization writes over the beginning
  of it. `CoverageBytes` became `CoverageByteCount`, because what it returns is the number of bytes of that buffer.
- The order in which a game releases its device objects lived in the game and had to be repeated by every game: it is a
  `GameShutdown` call now, which the assemblies of the engine fill in.

## [0.2.0] - 2026-10-07

### Added

- `IFontService` and `FontHandle`, so a game loads a TrueType or OpenType font at a height and draws text with it:
  `Load` bakes and caches the glyph atlas, `Measure` reports what a line advances and how tall it is, and `Draw` issues
  one textured quad per glyph through `IRenderer.DrawTextureRegion`. The built-in bitmap font stays as the zero-config
  fallback of `IRenderer.DrawText`. A disposed service refuses to load another font, while unloading what a renderer
  refused to release stays available.
- The sample content ships `Cousine-Regular.ttf`, a monospaced font by Steve Matteson under the SIL Open Font License
  1.1, which the font tests bake and the sample draws text with; the license text and the source are recorded next to it.
- `TrueTypeFontBake` and `GlyphPacking`, which rasterize the glyphs of a TrueType or OpenType font into one atlas with
  `StbTrueTypeSharp`, so text stops being limited to the built-in 8x8 bitmap font; the glyph atlas is a texture like any
  other, so it is tinted and drawn through `IRenderer.DrawTextureRegion`. A glyph box has to be a finite size, a cell may
  not be wider than 8192 pixels and an atlas may not need more than 16 million pixels: anything beyond that is rejected
  before a buffer is allocated or the rasterizer is called.
- `IRenderer.DrawTextureRegion`, which maps a part of a texture onto a quad, so an atlas, a sprite sheet or a glyph
  bitmap reaches the screen; `DrawSprite` is the same call with the region that covers the whole texture.
- `IAssetLoader.Load{T}` gained an overload that takes a `JsonTypeInfo{T}` of a source generated context, so a game can
  read its own assets in an AOT build, where the reflective overload is trimmed away.
- `IRenderer` is disposable: `SilkRenderer` deletes its shader program, its buffers and the font atlas, so closing a
  window, or attaching a renderer a second time, no longer leaks video memory. Dispose the renderer before the window is
  closed, while the OpenGL context is alive.
- Camera culling: `Camera2D.ViewportSize` is the rectangle that the camera covers (`Camera2D.VisibleWorld`), and
  `RenderSystem` skips a sprite whose box, rotated by its transform, does not overlap it. A camera without a viewport
  size culls nothing, so a game that never set it keeps drawing everything.
- Sprite rotation: `IRenderer.DrawSprite` takes the angle of the quad around its centre and `RenderSystem` passes the
  rotation of the transform, so `TransformComponent.Rotation` is drawn instead of ignored; `SpriteQuad` is the geometry
  behind it and is tested without a device.
- `World.Borrow{T}` and `ComponentRef{T}`, a checked borrow of a component that validates the slot generation on every
  read and write, so an identifier that outlived its entity cannot reach the component of the entity that took the slot.
- `IRenderPass` and `RenderPipeline`, so the passes of a frame, the world and the UI, are an ordered list that a game
  fills once, instead of two calls that had to be kept in order by hand; `RenderSystem` and `UIRenderSystem` are passes.
- `FixedTimestep` and `IGameLoop.Run(update, render)`, so a simulation advances by a fixed step whatever the frame rate
  is while the frame is drawn once, with a limit on the time that a single frame may contribute and `Alpha` left over
  for interpolation.
- Scene serialization: `ISceneSerializer` and the `ComponentRegistry` that every assembly fills with its own
  components, so a world is saved, loaded and edited as JSON.
- `ISoundService` and `SoundHandle`, so a game loads a sound once and plays it from anywhere; `IAudioService` gained
  `CreateSound`/`DeleteSound` for the device buffers, and `OpenALAudioService` with `AddAgeOpenALAudio` plays them.
- `ISoundLoader` with `SoundLoader`, which reads the header of a file and decodes RIFF WAVE (including 8, 24 and 32 bit
  and IEEE float samples), MPEG audio through NLayer and Ogg Vorbis through NVorbis, all of it into 16-bit samples.
- Window icon and `SplashScreen`, so a game opens with the branding of the engine: the icon can be replaced through
  `IWindowService.SetIcon`, and the splash takes a logo of its own, scales it or switches off.
- `IRenderer.CreateTexture`/`ReleaseTexture` and `ITextureService`, so decoded images reach the GPU and are cached by path; the sample draws a texture from `content/`.
- `SilkInputService` and `AddAgeSilkInput`, so a game reads keyboard and mouse from the window; `IInputService` gained `BeginFrame`.
- `IAssetLoader.Load<T>` for UTF-8 text, raw bytes and JSON assets.
- `ResourceHandle` and `ResourcePool<T>`, the shared versioned store behind engine resources.
- `IImageLoader` for decoding PNG, JPEG, BMP, TGA and GIF images into RGBA pixels.

### Changed

- The sample shows what this release brought: the second sprite spins, both sprites tint while their colliders touch,
  `E` spawns a short-lived sprite, and the HUD, drawn with the TrueType font of the content folder, reports the engine
  version, the live entity count, the contacts of the frame and which entity was spawned last, with its generation.
- `ResourcePool.Clear` runs the release callback of a slot after the lock was released, so a slow callback, or one that
  calls back into the pool, no longer holds the pool against other threads. The bookkeeping itself stays under the lock,
  and the call walks a snapshot, so a resource that another thread adds while it runs stays live.
- `ResourcePool` guards its slots, its path index and its counter with a lock, so a background loader cannot corrupt the
  pool while the thread that owns the device creates and deletes the resources. The payloads and the device objects
  themselves stay unsynchronized, and the documentation says who owns them.
- `World` states its threading model: its storage is not synchronized and the world belongs to the thread that runs the
  loop, so a game that touches one from more than one thread serializes the calls itself, and the generation checks of an
  identifier or a borrow are correctness in sequential use rather than a concurrency guarantee. `SystemPipeline` says
  that it runs its systems in order, on the thread that calls it.
- `World.GetRef` documents the lifetime of the reference it hands out in one place: any structural change to the entity
  invalidates it, the storage of its slot is handed out again, and a reference held across the destruction of its entity
  therefore reaches the component of the next entity that takes the slot. `World.Borrow` is the checked alternative.
- `World` hands the slot of a destroyed entity out again in a new generation and `Entity` carries that generation, so
  the storage no longer grows with every entity a long session ever had, and an identifier that outlived its entity is
  no longer alive even when the slot was taken by another entity afterwards.

- `CollisionSystem` keeps its spatial hash between frames instead of rebuilding it: a box that did not move stays in
  its cells, a moved one changes them, the entries of destroyed entities are dropped, and a cell that ends up empty is
  removed. `CellCount` reports how many cells are left.

## [0.1.0] - 2026-10-06

### Added

- Entity component system core with `World`, `Entity` and `SystemPipeline`.
- Math primitives: `Vector2`, `Color`, `Matrix4x4`, `Rect`, `Aabb`.
- `BitmapFontMetrics` describing the built-in 8x8 font grid.
- Game loops: `IGameLoop`, `TestGameLoop` and `SilkGameLoop`.
- Rendering: `SilkWindowService`, `SilkRenderer`, `RenderSystem` and
  `UIRenderSystem`.
- Input: `IInputService`, `NullInputService`, `Key` and `MouseButton`.
- Audio: `IAudioService` and `NullAudioService`.
- Physics: `CollisionSystem` with a rebuild-per-frame spatial hash.
- UI: `UIUpdateSystem` and the retained UI components.
- Assets: sandboxed `IAssetLoader` and `NullAssetLoader`.
- Dependency injection extensions for every assembly.
- CI, release, documentation, labeling workflows and issue templates.

### Fixed

- `Camera2D.GetViewMatrix` scaled before translating, so the camera position only
  landed on the viewport origin at unit zoom, and `Matrix4x4.CreateTranslation`
  wrote the translation to the last column while the rest of the pipeline treats
  vectors as rows.
- The renderer multiplied the projection matrix on the left, which sent the
  camera translation into `w` and collapsed the world onto a diagonal.
- `UIRenderSystem` treated a button and a label as mutually exclusive, so a
  button with a caption never drew its text.
