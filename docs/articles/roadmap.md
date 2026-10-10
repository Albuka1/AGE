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
An interface is authored against a resolution of its own: a canvas scales it to the window, the anchors of an element keep
it where it belongs at any size or shape of a window, and a camera that fits a design area to a window does the same for the
world. A shader of a game draws with the frame it is drawn into — the world, the text and the interface as they stand — and a
render target is what holds a layer of a frame that is processed more than once. A sprite is drawn from layers of its own, each
of them naming an image and the material that draws it, and a material is content of its own: a document holds the stages of a
program and the values its uniforms start with, so a hundred sprites that shine the same way are a hundred names of one material.
A build reads its content in passes, and a mistake is grouped under the pass that found it, so a person who is told to fix a sheet
is not sent looking at a string of a language. The renderer can clip what it draws: a rectangle is pushed and popped, the clips
nest, and what follows a push is kept inside it, which is what a scrollable panel and a list longer than its box need.

What remains are the content features the engine does not have yet, and then the platform work.

## Features

- UI widgets beyond a button and a label — slider, drop-down, check box, text field, scrollable panel — which the anchors, the
  scaler of a canvas, the pointer test of the interface and the clip of the renderer are already in place for. The widgets are
  components rather than kinds of entity: a slider is a component a game attaches to whatever it wants to slide, and the same
  holds for the rest of them, so a game lays its settings out with them instead of writing each one again.
- A window system of its own, and the dev window built on it: a window that stands apart from the frame — movable, resizable,
  in front of the game — a set of such windows, and tabs inside a window, so the console, the tree of the interface and the
  textures each get a tab instead of a place on one strip over the game.
- 2D lighting: a light that a sprite or the world is drawn under, which the renderer does not have today.
- Positional audio: voices whose position can change, a listener that follows the camera, and the falloff between them.
- Hierarchy.
- OBB collision and multiple contacts.
- A splash screen that works: the resources and the systems of the engine and of a game load while the logo is on screen, with
  a bar under it, instead of a window that stands empty until everything is ready.
- The graphics settings of a game as settings: vertical sync, a limit on the frames of a second, the mode of the window and a
  scale, as console variables.
- The logs of the engine and of a game in the console, coloured by their level: an error in red, a warning in yellow, the rest
  in the plain colour.
- A console of its own: the completion of a command that is being typed, the value and the description of a setting in a
  tooltip, and the strings of both of them read from the content like every other string of a game.
- Text beyond fitting a line into a box: the kerning of a font is not read, one text carries one style, and a script that is
  written right to left or a font whose glyphs are colored pictures needs more than the layout has today.
- A material is shared by name and not by value: what is left of it is the values a frame computes, a uniform that a pass sets
  while it runs, and two stages of one sprite that read one set of uniforms rather than two documents that declare the same one.

## Platform

- AOT.
- AssemblyLoadContext isolation for plugins.
