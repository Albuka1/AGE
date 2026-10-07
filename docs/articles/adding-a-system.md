# Adding a system

A system is any type that implements `ISystem`:

```csharp
public sealed class MovementSystem : ISystem
{
    private readonly float _speed;

    public MovementSystem(float speed) => _speed = speed;

    public void Update(World world, in GameTime time)
    {
        foreach (Entity entity in world.Enumerate<TransformComponent>())
        {
            ref TransformComponent transform = ref world.GetRef<TransformComponent>(entity);
            transform.Position += new Vector2(_speed * (float)time.Delta, 0f);
        }
    }
}
```

Register it from dependency injection or manually:

```csharp
SystemPipeline pipeline = provider.GetRequiredService<SystemPipeline>();
pipeline.Add(new MovementSystem(120f));
```

## Ordering and lifetime

`SystemPipeline.Add` appends in call order and is not idempotent: adding the
same instance twice runs it twice per update step. A pipeline is a singleton per
application and must not be shared between worlds.

## Component access rules

- `Get<T>` throws when the entity is not alive or the component is absent. Use
  `Has<T>` to check first.
- `GetRef<T>` returns a reference into the component storage. The reference is
  invalidated by a later `Set<T>` or `CreateEntity` for the same component
  type, because storage may be reallocated.
- Components must be structs; boxing is not allowed.
