# Progress

What of the plan is already in the code, and what is left. This file is **tracked by git** on purpose: the planning
documents of this repository (`RISKS.md`, `AGE-PLAN-PRIORITY.md`, `AGE-DESIGN-DECISIONS.md`) are listed in
`.git/info/exclude` and are never committed, so nothing written in them survives a fresh clone or a later session. If a
statement about progress matters, it belongs here.

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

## Next

**B1 (rest): a component that names a parent, a walk of the tree, and the order "a parent before its child".** Neither
gate blocks it.

The one thing to check on its own: a reference from a child to its parent is exactly the kind of reference A2 warns
about, one that leads nowhere after a load. So it needs a round trip through `SceneSerializer`, not a test of the
behaviour at runtime alone.
