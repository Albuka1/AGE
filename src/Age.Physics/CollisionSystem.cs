using Age.Core;

namespace Age.Physics;

/// <summary>
/// Detects overlapping colliders and resolves them into <see cref="CollisionComponent"/> instances.
/// </summary>
public sealed class CollisionSystem : ISystem
{
    private const float CellSize = 64f;

    private readonly List<CollisionPair> _pairs = new();
    private readonly List<Entity> _stale = new();
    private readonly Dictionary<int, Aabb> _boxes = new();
    private readonly Dictionary<(int X, int Y), List<Entity>> _cells = new();
    private readonly HashSet<(int A, int B)> _seen = new();

    /// <summary>Gets the collision pairs that were produced by the most recent update.</summary>
    public IReadOnlyList<CollisionPair> LastPairs => _pairs;

    /// <summary>Detects the colliders that overlap in the given world and republishes the result.</summary>
    /// <param name="world">The world to scan.</param>
    /// <param name="time">The frame time. Collision detection does not use it.</param>
    /// <remarks>
    /// The update runs in three steps. First every <see cref="CollisionComponent"/> is removed, so the result never
    /// mixes with the previous frame. Then a spatial hash is rebuilt from the <see cref="ColliderComponent"/> boxes,
    /// with a fixed cell size, which keeps the candidate pairs local, and the candidates in a cell are tested with
    /// <see cref="Aabb.Intersects"/>. Finally each overlapping pair is added to <see cref="LastPairs"/> and a
    /// <see cref="CollisionComponent"/> is attached to both entities, keeping the first partner of each entity.
    /// </remarks>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        RemoveStale(world);
        _pairs.Clear();
        BuildHash(world);
        BuildPairs(world);
    }

    /// <summary>
    /// Removes the collision components of the previous update. The entities are buffered first, because the world must
    /// not change while <see cref="World.Enumerate{T}"/> walks it.
    /// </summary>
    private void RemoveStale(World world)
    {
        _stale.Clear();

        foreach (Entity entity in world.Enumerate<CollisionComponent>())
        {
            _stale.Add(entity);
        }

        foreach (Entity entity in _stale)
        {
            world.Remove<CollisionComponent>(entity);
        }
    }

    private void BuildHash(World world)
    {
        _boxes.Clear();
        _cells.Clear();

        foreach (Entity entity in world.Enumerate<ColliderComponent>())
        {
            if (!world.Has<TransformComponent>(entity))
            {
                continue;
            }

            TransformComponent transform = world.Get<TransformComponent>(entity);
            ColliderComponent collider = world.Get<ColliderComponent>(entity);
            Aabb box = Aabb.FromRect(new Rect(transform.Position + collider.Offset, collider.Size * transform.Scale));

            _boxes[entity.Id] = box;

            int minX = CellCoordinate(box.Left);
            int minY = CellCoordinate(box.Top);
            int maxX = CellCoordinate(box.Right);
            int maxY = CellCoordinate(box.Bottom);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var key = (x, y);
                    if (!_cells.TryGetValue(key, out List<Entity>? bucket))
                    {
                        bucket = new List<Entity>();
                        _cells[key] = bucket;
                    }

                    bucket.Add(entity);
                }
            }
        }
    }

    private void BuildPairs(World world)
    {
        _seen.Clear();

        foreach (List<Entity> bucket in _cells.Values)
        {
            for (int first = 0; first < bucket.Count - 1; first++)
            {
                for (int second = first + 1; second < bucket.Count; second++)
                {
                    Entity a = bucket[first];
                    Entity b = bucket[second];

                    if (!_seen.Add((a.Id, b.Id)) || !_boxes[a.Id].Intersects(_boxes[b.Id]))
                    {
                        continue;
                    }

                    _pairs.Add(new CollisionPair(a, b));
                    Attach(world, a, b);
                    Attach(world, b, a);
                }
            }
        }
    }

    private static void Attach(World world, Entity entity, Entity other)
    {
        if (!world.Has<CollisionComponent>(entity))
        {
            world.Set(entity, new CollisionComponent { Other = other });
        }
    }

    private static int CellCoordinate(float value) => (int)MathF.Floor(value / CellSize);
}
