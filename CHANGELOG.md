# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Deferred operations on a world, for the code that runs while the world is being enumerated:
  `World.RequestCreate`, `RequestDestroy`, `RequestSet{T}` and `RequestRemove{T}` queue an operation, and
  `ApplyPending` applies the queue in the order it was received. `World.Update` and `World.UpdateFrame` apply it before
  and after the systems of the step, so a request made by a system or a frame system takes effect before the next one.
  An entity whose destruction was requested is not alive any more for the rest of the step, so `IsAlive` and `Has{T}`
  already report it as gone while its components stay in place until the queue is applied, and neither `Enumerate` nor
  `Enumerate{T}` visits it. Creation is queued as well: `RequestCreate` reserves the identifier right away, so the
  requests that follow it can already use it, and the entity becomes part of the world when the queue is applied. A
  write that was requested for an entity that is destroyed earlier in the same batch is dropped. Destroying a reserved
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
