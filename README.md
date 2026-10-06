<p align="center">
  <img alt="AGE" width="250" src="docs/images/logo-big.svg" />
</p>

# Auae Game Engine (AGE)

A small 2D game engine for .NET 10, built around an entity-component-system
core, a Silk.NET rendering backend and a dependency-injection friendly service
model.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-blue)

## Overview

AGE is organized as a set of focused assemblies:

| Project | Responsibility |
| ------- | -------------- |
| `Age.Core` | Entities, components, systems, math primitives, game loop |
| `Age.Assets` | Sandboxed path-based asset access and loading |
| `Age.Input` | Keyboard and mouse abstraction |
| `Age.Audio` | Sound playback abstraction |
| `Age.Physics` | Axis-aligned collision detection with a spatial hash |
| `Age.UI` | Retained UI components and pointer interaction |
| `Age.Rendering` | Silk.NET window, OpenGL renderer, render systems and window input |
| `Age.Sample` | Console sample that wires everything together |
| `Age.Tests` | Behavioural unit tests |

## Requirements

- .NET SDK 10.0.100 or later
- A GPU with OpenGL 3.3 core profile support to run `Age.Sample`

## Getting started

```bash
git clone https://github.com/Albuka1/Age.git
cd Age
dotnet build Age.slnx -c Release
dotnet test tests/Age.Tests/Age.Tests.csproj -c Release
dotnet run --project samples/Age.Sample/Age.Sample.csproj
```

See [docs/articles/quickstart.md](docs/articles/quickstart.md) for a guided tour.

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

The following work is planned but not part of the current foundation:

- Scene serialization (JSON)
- User-defined shaders and materials
- StbTrueTypeSharp
- `OpenALAudioService`
- Entity id pool
- Incremental spatial hash
- OBB collision
- Multiple contacts
- Camera culling
- AOT
- AssemblyLoadContext isolation for plugins

## Contributing

Changes are made through pull requests against `main`. See
[CONTRIBUTING](CODE_OF_CONDUCT.md) and the pull request template for details.

## License

MIT. See [LICENSE](LICENSE).
