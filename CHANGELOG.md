# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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

- `World` hands the slot of a destroyed entity out again in a new generation and `Entity` carries that generation, so
  the storage no longer grows with every entity a long session ever had, and an identifier that outlived its entity is
  no longer alive even when the slot was taken by another entity afterwards.

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
