# Resources

Every asset the repository ships lives here, split by kind and by section, so a file is stored once and reaches every
project that needs it.

## Where a file goes

A path is written the same way everywhere: in the engine, in a document of content and in this table.

- `<Section>/<Subsection>/<file>`, relative to this folder, with the first segment in capitals: `Textures/Tiles/tiles.bmp`.
- A section is the kind of asset (`Textures`, `Audio`, `Fonts`, `Prototypes`), a subsection is a group inside it, and a
  document of content names a resource with exactly that path — which is what `Age.Content.Lint` checks against the files
  of a build, so a path with a typo in it fails a build rather than a fight.
- A sprite sheet is a document named like the image beside it, so `Textures/Entities/goblin.yml` is the sheet of
  `Textures/Entities/goblin.tga`: it declares `version` (the format this build reads), `image`, `cell`, `columns`, `rows`,
  `states`, and `license` with `copyright` for art that came from somewhere else. A state gives the length of a frame with
  `delay` for every frame of it, or the length of each frame with `delays`. Every document of YAML under the textures of a
  build is a sheet of it, and the linter reads all of them: the image a document names and the grid it declares over it, the
  version, where its art comes from, and every state a prototype names — on the sprite or on its animation.
- A section appears with its first file, so nothing is committed as an empty promise.

## What is here

| Folder | Contents | Used by |
| --- | --- | --- |
| `Audio/Effects` | `click.wav`, the short sound effect a press plays | `Age.Sample` |
| `Audio/Samples` | `sample.ogg` and `sample.mp3`, two seconds of two real recordings that the decoders read, with a `README.md` that says where they come from | `Age.Tests` |
| `Fonts` | `Cousine-Regular.ttf`, the TrueType font the text tests bake and the sample draws with, with its `OFL.txt` license and its own `README.md` | `Age.Sample`, `Age.Tests` |
| `Locale/en/Entities` | `creatures.yml`, the names and the descriptions of the creatures in the base language | `Age.Content`, `Age.Tests` |
| `Locale/en/Items` | `items.yml`, the names and the descriptions of the items in the base language | `Age.Content`, `Age.Content.Lint`, `Age.Tests` |
| `Locale/en/Ui` | `window.yml`, the strings of the window in the base language, including one that is written by count | `Age.Content`, `Age.Tests` |
| `Locale/ru/Entities` | `creatures.yml`, the same keys in Russian | `Age.Tests` |
| `Locale/ru/Items` | `items.yml`, the same keys in Russian | `Age.Tests` |
| `Locale/ru/Ui` | `window.yml`, the same keys in Russian, with the three forms its counts are written in | `Age.Tests` |
| `Prototypes/Entities` | `base.yml`, which declares `CreatureBase`, and `goblin.yml`, which declares `Goblin` and inherits it | `Age.Sample`, `Age.Tests`, `Age.Content.Lint` |
| `Prototypes/Items` | `base.yml`, which declares `ItemBase`, and `sword.yml`, which declares `Sword` and inherits it | `Age.Sample`, `Age.Tests`, `Age.Content.Lint` |
| `Textures/Entities` | `goblin.tga`, the frames of the goblin in a grid of four cells by two, and `goblin.yml`, the document that says where each state lies on it | `Age.Sample`, `Age.Tests`, `Age.Content.Lint` |
| `Textures/Icons` | `icon-20.png`, `icon-40.png` and `icon-60.png`, the window icon in the three sizes the operating system picks from | `Age.Rendering` |
| `Textures/Logo` | `logo-320.png`, the logo the splash screen shows | `Age.Rendering` |
| `Textures/Tiles` | `tiles.bmp`, the tile the sprite of the sample draws | `Age.Sample` |

The table holds every file of this folder, and a test walks the folder and checks it: a file that no line mentions fails
the build, and a path this file mentions that is not there fails it too. That is what keeps this catalogue from going
stale in silence.

## Sections that are not here yet

A section is created with its first file; until then this table says where it will go.

| Section | Will hold | Comes with |
| --- | --- | --- |
| `Maps` | A map as a file of its own, when a map is authored rather than saved during a run | A map a person authors |
| `Ui` | Layout documents of the interface | B6 |
| `Shaders` | Shader sources, when a pass needs one of its own | — |

## How a project uses it

`Directory.Build.props` points `$(AgeResources)` at this folder, so a project reaches it without depending on where
the project itself sits:

- `Age.Rendering` embeds the branding images with `EmbeddedResource` and an explicit `LogicalName`, which is what
  makes them addressable as `Age.Rendering.Resources.Textures.Icons.icon-20.png` and
  `Age.Rendering.Resources.Textures.Logo.logo-320.png`.
- `Age.Sample` and `Age.Tests` copy the folder next to their output under the same `Resources` name and hand that
  path to `IAssetLoader.Initialize`, so the path a game loads is written relative to this folder:
  `Fonts/Cousine-Regular.ttf`, `Textures/Tiles/tiles.bmp`, `Audio/Effects/click.wav`. The sample leaves
  `Audio/Samples` out, because the decoder fixtures belong to the test project rather than to the game.
- `Age.Content.Lint` reads the prototypes of this folder in CI, with the same loader a game uses: a document that names
  a file which this folder does not hold fails the build.

A file that changes here changes for every consumer at once, so there is nothing to keep in step. The art of this
repository is MIT, like the rest of it, and a file that came from somewhere else says where it came from in the document
beside it: `license` and `copyright`, which `Age.Content.Lint` requires of every sheet, so a build can answer what it
ships.

The licenses of the files that need attribution travel next to them: `Fonts/OFL.txt` for the font, and
[`Audio/Samples/README.md`](Audio/Samples/README.md) for the two recordings.
