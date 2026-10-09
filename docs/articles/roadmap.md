# Roadmap

The foundation covers the entity-component core, the game loops, the OpenGL 3.3 renderer,
collision detection, retained UI, sandboxed asset loading with image and sound decoding, textures
that are loaded and cached by path, sounds that play through OpenAL, scenes that are saved and
loaded as JSON, the versioned resource pool, a window-backed input service, a simulation that
advances by a fixed step, entity slots that are reused in a new generation, render passes that a
pipeline orders, a spatial hash that follows the boxes that moved, a camera that culls what it cannot
see, a renderer that turns a sprite by the rotation of its transform and releases its device objects,
and an asset loader that reads a generated contract where a build is trimmed, so a game draws text in a font of its own
instead of the built-in one. The strings of a game are content of their own: a key is answered in the language the game
plays in, and the text of a world and of the interface is drawn from it, in a font baked for the script of that language.

What remains are the content features the engine does not have yet, and then the platform work.

## Features

- Sprite sheets and animation: slicing an atlas into frames and named regions, and a system that advances them.
- UI widgets beyond a button and a label — slider, drop-down, check box, text field, scrollable panel — with anchors.
- Positional audio: voices whose position can change, a listener that follows the camera, and the falloff between them.
- Hierarchy.
- User-defined shaders and materials.
- OBB collision and multiple contacts.

## Platform

- AOT.
- AssemblyLoadContext isolation for plugins.
