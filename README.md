<p align="center">
  <img alt="AGE" width="250" src="docs/images/logo-big.svg" />
</p>

# Auae Game Engine (AGE)

A small 2D game engine for .NET 10, built around an entity-component-system
core, a Silk.NET rendering backend and a dependency-injection friendly service
model.

Documentation: [albuka1.github.io/AGE](https://albuka1.github.io/AGE/) — API reference, quickstart and roadmap.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-blue)

## Overview

AGE is organized as a set of focused assemblies:

| Project | Responsibility |
| ------- | -------------- |
| `Age.Core` | Entities, components, systems, math primitives, game loop |
| `Age.Assets` | Sandboxed path-based asset access and loading |
| `Age.Input` | Keyboard and mouse abstraction |
| `Age.Audio` | Sound resources, WAV decoding and playback through OpenAL |
| `Age.Physics` | Axis-aligned collision detection with a spatial hash |
| `Age.UI` | Retained UI components and pointer interaction |
| `Age.Rendering` | Silk.NET window, OpenGL renderer, render systems, window input, window icon and splash screen |
| `Age.Sample` | Console sample that wires everything together |
| `Age.Tests` | Behavioural unit tests |

## Requirements

- .NET SDK 10.0.100 or later
- A GPU with OpenGL 3.3 core profile support to run `Age.Sample`
- An audio device with OpenAL for the sound of `Age.Sample`; without one, drop `AddAgeOpenALAudio` and it stays silent

## Getting started

```bash
git clone https://github.com/Albuka1/Age.git
cd Age
dotnet build Age.slnx -c Release
dotnet test tests/Age.Tests/Age.Tests.csproj -c Release
dotnet run --project samples/Age.Sample/Age.Sample.csproj
```

See [docs/articles/quickstart.md](docs/articles/quickstart.md) for a guided tour, or the
[published documentation](https://albuka1.github.io/AGE/) for the API reference.

## Architecture

- A `World` owns entity identifiers and component storage. Components are
  structs that implement `IComponent`; storage is a
  `Dictionary<Type, Array>` keyed by component type.
- `SystemPipeline` runs an ordered list of `ISystem` instances once per frame.
- Services are resolved from dependency injection. `World` is deliberately not
  registered there because its lifetime belongs to the caller.
- The renderer is the only component that touches OpenGL. It is never
  instantiated by the test suite.

## Roadmap

The following work is planned but not part of the current foundation. The order is what a real
game needs first: the entries at the top block building one, the rest make the engine hold up
under load and cover the limits of the current design.

- `OpenALAudioService`, with a sound resource to play; textures already follow the resource pattern
  it will reuse.
- Scene serialization (JSON).
- Fixed timestep in the game loop.
- Entity id pool and generations for stale handles.
- Render passes ordered by the loop instead of by hand.
- Incremental spatial hash.
- Camera culling.
- StbTrueTypeSharp.
- User-defined shaders and materials.
- OBB collision and multiple contacts.
- Sprite rotation, so `TransformComponent.Rotation` is not ignored.
- Asset cache and AOT friendly JSON.
- AOT.
- AssemblyLoadContext isolation for plugins.

## Contributing

Changes are made through pull requests against `main`. See
[CONTRIBUTING](CODE_OF_CONDUCT.md) and the pull request template for details.

## License

MIT. See [LICENSE](LICENSE).
