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
    private readonly List<Entity> _dropped = new();
    private readonly Dictionary<Entity, Aabb> _boxes = new();
    private readonly Dictionary<(int X, int Y), List<Entity>> _cells = new();
    private readonly HashSet<(int A, int B)> _seen = new();

    /// <summary>Gets the collision pairs that were produced by the most recent update.</summary>
    public IReadOnlyList<CollisionPair> LastPairs => _pairs;

    /// <summary>Gets the number of cells of the spatial hash that the most recent update left non-empty. A cell is dropped as soon as it holds no box.</summary>
    public int CellCount => _cells.Count;

    /// <summary>Detects the colliders that overlap in the given world and republishes the result.</summary>
    /// <param name="world">The world to scan.</param>
    /// <param name="time">The frame time. Collision detection does not use it.</param>
    /// <remarks>
    /// The update runs in three steps. First every <see cref="CollisionComponent"/> is removed, so the result never
    /// mixes with the previous frame. Then the spatial hash, a fixed cell size, is brought in line with the colliders of
    /// the world: the entries that are gone are dropped and the ones that appeared or moved change cells, while a box
    /// that did not move keeps the cells it was in. Finally the candidates that share a cell are tested with
    /// <see cref="Aabb.Intersects"/>, each overlapping pair is added to <see cref="LastPairs"/>, and a
    /// <see cref="CollisionComponent"/> is attached to both entities, keeping the first partner of each entity.
    /// </remarks>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        RemoveStale(world);
        _pairs.Clear();
        RefreshHash(world);
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

    /// <summary>
    /// Brings the spatial hash in line with the colliders of the world: the entries of the entities that are gone are
    /// dropped, and the ones that appeared or moved move to the cells of their new box, while a box that did not move
    /// keeps the cells it was in. A still world therefore costs one pass over the colliders instead of a rebuild of
    /// every cell. The entities that are gone are buffered first, because the dictionary cannot change while it is read.
    /// </summary>
    private void RefreshHash(World world)
    {
        _dropped.Clear();

        foreach (Entity entity in _boxes.Keys)
        {
            if (!world.IsAlive(entity) || !world.Has<ColliderComponent>(entity) || !world.Has<TransformComponent>(entity))
            {
                _dropped.Add(entity);
            }
        }

        foreach (Entity entity in _dropped)
        {
            RemoveFromCells(entity, _boxes[entity]);
            _boxes.Remove(entity);
        }

        foreach (Entity entity in world.Enumerate<ColliderComponent>())
        {
            if (!world.Has<TransformComponent>(entity))
            {
                continue;
            }

            Aabb box = BoxOf(world, entity);

            if (_boxes.TryGetValue(entity, out Aabb previous))
            {
                if (SameBox(previous, box))
                {
                    continue;
                }

                RemoveFromCells(entity, previous);
            }

            _boxes[entity] = box;
            AddToCells(entity, box);
        }
    }

    private static Aabb BoxOf(World world, Entity entity)
    {
        TransformComponent transform = world.Get<TransformComponent>(entity);
        ColliderComponent collider = world.Get<ColliderComponent>(entity);
        return Aabb.FromRect(new Rect(transform.Position + collider.Offset, collider.Size * transform.Scale));
    }

    private void AddToCells(Entity entity, Aabb box)
    {
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

    /// <summary>Removes the entity from the cells of an old box and drops the cells that become empty.</summary>
    private void RemoveFromCells(Entity entity, Aabb box)
    {
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
                    continue;
                }

                bucket.Remove(entity);

                if (bucket.Count == 0)
                {
                    _cells.Remove(key);
                }
            }
        }
    }

    private static bool SameBox(Aabb left, Aabb right) =>
        left.Left == right.Left && left.Top == right.Top && left.Right == right.Right && left.Bottom == right.Bottom;

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

                    // A bucket no longer keeps the entity order, because a moved box is appended again, so the pair is
                    // put in identifier order. That is the order of LastPairs, and it lets the seen set reject a pair
                    // that the two entities share through more than one cell.
                    if (a.Id > b.Id)
                    {
                        (a, b) = (b, a);
                    }

                    if (!_seen.Add((a.Id, b.Id)) || !_boxes[a].Intersects(_boxes[b]))
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
