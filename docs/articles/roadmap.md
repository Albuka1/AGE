# Roadmap

The foundation covers the entity-component core, the game loops, the OpenGL 3.3 renderer,
collision detection, retained UI, sandboxed asset loading with image and sound decoding, textures
that are loaded and cached by path, sounds that play through OpenAL, scenes that are saved and
loaded as JSON, the versioned resource pool, a window-backed input service, a simulation that
advances by a fixed step, entity slots that are reused in a new generation, render passes that a
pipeline orders, and a spatial hash that follows the boxes that moved.

What remains is what the current design cannot do, and then the platform work.

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
