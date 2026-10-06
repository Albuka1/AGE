# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
