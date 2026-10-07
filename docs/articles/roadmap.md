# Roadmap

The foundation covers the entity-component core, the game loops, the OpenGL 3.3 renderer,
collision detection, retained UI, sandboxed asset loading with image decoding, the versioned
resource pool, and a window-backed input service.

The order below is what a real game needs first: the first group blocks building one, the second
keeps a project stable as it grows, the third covers the limits of the current design, and the
last is platform work.

## Blocks building a game

- Texture upload in `IRenderer` and a texture service on top of it, so decoded images reach the
  GPU; the same resource pattern serves the sound service next.
- `OpenALAudioService`, with a sound resource to play.
- Scene serialization (JSON), so a world can be saved, loaded and edited.

## Stability and scale

- Fixed timestep in `IGameLoop`, so simulation stops depending on the frame rate.
- Entity id pool with a generation, so long sessions do not grow and stale handles stay detectable.
- Render passes ordered by the loop instead of by hand.
- Incremental spatial hash in `CollisionSystem`.

## Limits of the current design

- Camera culling.
- StbTrueTypeSharp, so text is not limited to the built-in 8x8 font.
- User-defined shaders and materials.
- OBB collision and multiple contacts.
- Sprite rotation, so `TransformComponent.Rotation` is not ignored.
- Asset cache and AOT friendly JSON.

## Platform

- AOT.
- AssemblyLoadContext isolation for plugins.
