# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `TrueTypeFontBake` and `GlyphPacking`, which rasterize the glyphs of a TrueType or OpenType font into one atlas with
  `StbTrueTypeSharp`, so text stops being limited to the built-in 8x8 bitmap font; the glyph atlas is a texture like any
  other, so it is tinted and drawn through `IRenderer.DrawTextureRegion`.
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
