# Progress

An internal working note: what of the plan is already in the code, what is left, and what is next. It is **tracked by
git** but deliberately **not published** — it is not in `toc.yml`, not in the docfx pages and not linked from the
README, because it is a document for working in, not for shipping. The planning documents of this repository
(`RISKS.md`, `AGE-PLAN-PRIORITY.md`, `AGE-DESIGN-DECISIONS.md`) are listed in `.git/info/exclude` and are never
committed at all, so nothing written in them survives a fresh clone. Any statement about progress that matters belongs
here.

## How to read this file

Verify before trusting it. An audit of the plan against this repository already found the plan listing work as undone
that was in fact implemented: the deferred operations of the world (A3) and the events (A4). The lesson is recorded
because it will happen again:

**Search by behaviour, not by the names the plan invented.** The plan calls the deferred operations a `CommandBuffer`;
no such type exists. The real names are `RequestCreate`, `RequestDestroy`, `RequestSet`, `RequestRemove` and
`ApplyPending` on `World`. A search for the name from the plan answers "not done" about finished work.

## Closed

| Step | What closed it |
| --- | --- |
| **A1** | Clock, pause, `TimeScale`; timers (`TimerComponent` / `TimerSystem`) and tweens (`TweenComponent` / `TweenSystem` with `TweenEasing`), each with tests |
| **A2** | References and identifiers in a scene, `SceneSerializer`, a version of the format |
| **A3** | The deferred operations of the world: `Request*` and `ApplyPending` |
| **A4** | Events: `IEventBus`, `ButtonPressedEvent`, `SpriteAnimationFinishedEvent` |
| **C1** | `PrototypeManager` with references, inheritance and `ProtoId` |
| **C2** | `EntityPrototype`; a scene is a prototype |
| **C3** | `Resources/Prototypes`, `Resources/Locale`, `Age.Content.Lint` |
| **C4** | Sprite sheets (`Age.Content.Sheets`) and animation (`SpriteAnimationSystem`) |
| **F4** | `Camera2D.ScreenToWorld` and `Camera2D.WorldToScreen` |
| **B1** (part) | `RectTransformComponent` with anchors, a pivot and `SizeDelta`; `CanvasComponent` with `IsRoot` and `CanvasScaleMode`; `UILayoutSystem` |
| **B2** (part) | A layout by anchors that holds at any resolution and at any scale of the canvas |
| **F1 / F2** (part) | A baked range of characters for a language, a stack of fonts, text fitted into a box, locales with plural forms |
| **F11** (part) | `ConsoleService` with commands and `CVarService`, and the console drawn |
| **F10** (part) | `ConsoleLoggerProvider` |
| **F8** (part) | `SplashScreen` and `BuiltInBranding` |

## Left

- **B1 (rest) — the hierarchy.** There is no tree. `UILayoutSystem` and `UIUpdateSystem` walk flat lists
  (`world.Enumerate<RectTransformComponent>()`, `_buttons`), and there is no component that names a parent. The word
  `Parent` in the code belongs to prototype inheritance in `Age.Content`, which is a different thing. This is first
  because B3 and B5 cannot be expressed without it.
- **B3 — clipping and scrolling.** `IRenderer` has no `PushClip` / `PopClip`. `TextOverflow.Clip` exists, but that
  drops lines in a text box; it does not clip a rectangle in the renderer.
- **B5 — the widgets.** A button and text are all there is: no slider, check box, list, progress bar or text field.
- **F8 (rest) — loading behind the logo.** The resources are not loaded while the logo is on screen.
- **F2 / B4** — behind the gates below.
- Anything else: positional audio, OBB collision, the graphics settings, the rest of the text work (kerning), a
  material shared by value, AOT, and `AssemblyLoadContext` isolation for plugins. `docs/articles/roadmap.md` carries
  the same list and is the tracked source for it.

## Gates

- **The language of the interface.** It decides whether F2 carries the character ranges of a script, but it does
  **not** block B1 or B3.
- **Whether the first version needs a text field.** It changes the weight of B4, but it does **not** block B1 or B3.
- **Drag and drop** — decided: it is not part of the first version, so this gate is closed.

## Left in the renderer

Everything else of the renderer is in place: the frame, the batches, the passes, render targets and the surface a
shader samples, sprite layers with a material each, text, and the interface with its canvas and its anchors. What is
left, in the order it is worth doing:

1. **Clipping and scrolling (B3).** `IRenderer` has no `PushClip` / `PopClip`, so a rectangle cannot be cut out of a
   draw. This is what a scrollable panel needs, and a list of items and recipes is nearly always longer than the
   screen. `TextOverflow.Clip` is not this: it drops lines in a text box and cuts nothing in the renderer. The
   hierarchy (B1) is not a prerequisite for the clip itself, only for a panel that clips its children.
2. **The widgets (B5).** A button and text are all there is. No slider, check box, list, progress bar or text field —
   and the inventory, the crafting and the pause menu are made of nothing but these. It is the largest remaining piece
   and it waits on the scrollable panel above.
3. **A material a frame writes into.** Today a material is the values a document declares. What is left is the values
   a frame computes — a uniform a pass sets while it runs — and two stages of one sprite that read one set of uniforms
   rather than two documents that declare the same one.
4. **The graphics settings of a game as settings.** No vertical sync, no limit on frames a second, no window mode and
   no scale: nothing of `VSync` / frame limit exists in the code. They belong among the console variables.
5. **A splash screen that loads.** `SplashScreen` and `BuiltInBranding` exist and draw the logo, but the resources are
   not loaded behind it, and there is no bar under it. A window still stands empty until everything is ready.
6. **The console of its own.** `ConsoleService` runs commands and the console is drawn; what is missing is the
   completion of a command being typed and the value and description of a setting in a tooltip, both read from the
   content like every other string.
7. **The logs coloured by their level.** `ConsoleLoggerProvider` writes the logs to the console; an error in red, a
   warning in yellow and the rest plain is not done. Nothing related to a log *level* carries a colour today.
8. **Text beyond fitting a line into a box.** The kerning of a font is not read, one text carries one style, and a
   script written right to left or a font whose glyphs are coloured pictures needs more than the layouter has today.
9. **The hierarchy (B1).** There is no tree: `UILayoutSystem` and `UIUpdateSystem` walk flat lists and no component
   names a parent. Listed last among the renderer work because the clip and the widgets are what a game feels first,
   but it is what makes a panel move with its children.

## Next

**B3 — `PushClip` / `PopClip` in `IRenderer` and `SilkRenderer`.** It is the smallest of the nine, it unblocks the
scrollable panel, and the panel is what the list, the inventory and the crafting are waiting on. Neither gate below
blocks it.

- The clip is a scissor rectangle on the device; the renderer already flushes a batch when the state it was collected
  under changes, so a clip that is pushed is one more thing that ends a batch.
- A clip has to nest, so it is a stack, and the rectangle of a push is the intersection of it with the one below.
- Clipping a *child* needs the hierarchy, which does not exist yet. The honest split is to land the clip on its own
  and let the panel that clips its children follow the hierarchy.
