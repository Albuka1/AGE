# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `IRenderer.CreateTexture`/`ReleaseTexture` and `ITextureService`, so decoded images reach the GPU and are cached by path; the sample draws a texture from `content/`.
- `SilkInputService` and `AddAgeSilkInput`, so a game reads keyboard and mouse from the window; `IInputService` gained `BeginFrame`.
- `IAssetLoader.Load<T>` for UTF-8 text, raw bytes and JSON assets.
- `ResourceHandle` and `ResourcePool<T>`, the shared versioned store behind engine resources.
- `IImageLoader` for decoding PNG, JPEG, BMP, TGA and GIF images into RGBA pixels.

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
