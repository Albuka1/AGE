# Roadmap

The current foundation covers the entity-component core, the game loops, the
OpenGL 3.3 renderer, collision detection and retained UI. The following work is
planned:

## Content pipeline

- Scene serialization (JSON)
- StbTrueTypeSharp integration

## Rendering

- User-defined shaders and materials
- Camera culling

## Input and audio

- `OpenALAudioService`

## Simulation

- Entity id pool
- Incremental spatial hash
- OBB collision
- Multiple contacts

## Platform

- AOT
- AssemblyLoadContext isolation for plugins
