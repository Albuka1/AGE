# Auae Game Engine

AGE is a compact 2D engine for .NET 10. It pairs an entity-component-system core with a
Silk.NET window and an OpenGL 3.3 core renderer.

## Start here

- [Quickstart](../QUICKSTART.md) — build a window, a world and a loop, then draw with
  the controls of the interface and read the content of a game.
- [API reference](../api/toc.yml) — every public type of every assembly of the engine,
  read from the XML documentation the build writes beside each one.

## What the engine is made of

| Assembly | What it holds |
| --- | --- |
| `Age.Core` | The world, the entities, the components, the systems, the scenes and the clock. |
| `Age.Assets` | The loader that sandboxes every path, the images and the sounds it decodes. |
| `Age.Content` | The prototypes a game is written as data, the sprite sheets, the strings of a language and the linter of the content. |
| `Age.Input` | The keyboard, the mouse, the typed characters and the clipboard. |
| `Age.Physics` | AABB colliders, the collision pairs of a step, and the events they raise. |
| `Age.Rendering` | The Silk.NET window, the OpenGL renderer, the passes of the world and of the interface, the text, the textures and the developer window. |
| `Age.UI` | The controls of the interface — a button, a label, a checkbox, a slider, a bar, a drop-down and a text field — and the systems that read the pointer. |
| `Age.Audio` | The sounds of a game, played through OpenAL. |

## Where the rest is written

- [`README.md`](https://github.com/Albuka1/Age/blob/main/README.md) — what the engine is
  and how to start it.
- [`CHANGELOG.md`](https://github.com/Albuka1/Age/blob/main/CHANGELOG.md) — what changed
  in each release.
- [`RELEASING.md`](https://github.com/Albuka1/Age/blob/main/RELEASING.md) — what the
  version numbers mean and how a release is cut.
