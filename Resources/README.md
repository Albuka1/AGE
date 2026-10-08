# Resources

Every asset the repository ships lives here, split by kind, so a file is stored once and reaches every project
that needs it.

| Folder | Contents | Used by |
| --- | --- | --- |
| `Audio/Effects` | Short sound effects | `Age.Sample` |
| `Audio/Samples` | Two seconds of two real recordings, one Ogg and one MP3, for the decoders | `Age.Tests` |
| `Fonts` | The TrueType font the text tests bake and the sample draws with, together with its license | `Age.Sample`, `Age.Tests` |
| `Textures/Icons` | The window icon, in the three sizes the operating system picks from | `Age.Rendering` |
| `Textures/Logo` | The logo the splash screen shows | `Age.Rendering` |
| `Textures/Tiles` | The tile the sprite of the sample is drawn with | `Age.Sample` |

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

A file that changes here changes for every consumer at once, so there is nothing to keep in step.

The licenses of the files that need attribution travel next to them: `Fonts/OFL.txt` for the font, and
[`Audio/Samples/README.md`](Audio/Samples/README.md) for the two recordings.
