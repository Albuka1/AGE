using Age.Core;

namespace Age.Physics;

/// <summary>
/// Detects overlapping colliders, resolves what a solid pair asks a resolution for, and reports every pair it found as a
/// contact that began, that stands, or that ended.
/// </summary>
/// <remarks>
/// <para>
/// The update runs in four steps. First the pairs of the last step are read into a buffer, so the contacts that are over
/// can be announced. Then the spatial hash is brought in line with the colliders of the world, the pairs that share a cell
/// are tested, and a pair is kept when each of the two colliders answers the layer of the other. The pairs of the step are
/// resolved, from the shortest penetration of each pair, unless both sides are immovable or one of them is a trigger.
/// Finally the pairs of the step are compared with the pairs of the step before, and one event of each of the three is
/// raised for every pair the comparison names: an enter, a stay or an exit.
/// </para>
/// <para>
/// The order of a resolution is the order of the pairs, which is the identifier of the first entity of a pair and then of
/// the second, so two runs with the same world resolve the same way. A body that was moved by one pair takes part in the
/// pairs that follow with the box it has now, which is what keeps a resolution from pushing a body back into another one
/// without ever settling.
/// </para>
/// <para>
/// A collider that is a trigger, and a body of a battle on a grid of cells is one, is reported and never resolved: what a
/// cell of a grid decides is where a body stands, and a box of a sprite is a picture of the body rather than its place.
/// </para>
/// </remarks>
public sealed class CollisionSystem : ISystem
{
    private const float CellSize = 64f;
    private const int ResolutionIterations = 2;

    private readonly List<CollisionPair> _pairs = new();
    private readonly List<Entity> _stale = new();
    private readonly List<Entity> _dropped = new();
    private readonly Dictionary<Entity, Aabb> _boxes = new();
    private readonly Dictionary<Entity, CollisionLayer> _masks = new();
    private readonly Dictionary<Entity, bool> _triggers = new();
    private readonly Dictionary<Entity, bool> _immovable = new();
    private readonly Dictionary<(int X, int Y), List<Entity>> _cells = new();
    private readonly HashSet<(Entity A, Entity B)> _seen = new();
    private readonly HashSet<(Entity A, Entity B)> _previous = new();
    private readonly HashSet<(Entity A, Entity B)> _current = new();

    /// <summary>Gets or sets a value indicating whether a solid pair is moved out of its overlap.</summary>
    /// <remarks>
    /// <para>
    /// The flag is on by default, because a system of collisions that only reports an overlap leaves two bodies standing
    /// in each other, which is what a game that draws a body where its box is cannot show. A game that only reads the
    /// pairs — a fight on a grid of cells, where every body is a trigger — pays for a resolution that never runs, because
    /// a pair of two triggers is left where it stands.
    /// </para>
    /// <para>
    /// A game that wants the pairs of a step as they are, without the world having changed by the time it reads them,
    /// sets this to <see langword="false"/>: the detection and the events are what the system does, and nothing of the
    /// world is moved.
    /// </para>
    /// </remarks>
    public bool Resolve { get; set; } = true;

    /// <summary>Gets the collision pairs that were produced by the most recent update.</summary>
    public IReadOnlyList<CollisionPair> LastPairs => _pairs;

    /// <summary>Gets the number of cells of the spatial hash that the most recent update left non-empty. A cell is dropped as soon as it holds no box.</summary>
    public int CellCount => _cells.Count;

    /// <summary>Detects the colliders that overlap in the given world, resolves what a pair asks for, and republishes the result.</summary>
    /// <param name="world">The world to scan.</param>
    /// <param name="time">The frame time. Collision detection does not use it.</param>
    public void Update(World world, in GameTime time)
    {
        ArgumentNullException.ThrowIfNull(world);

        RemoveStale(world);
        _pairs.Clear();
        _current.Clear();

        RefreshHash(world);
        BuildPairs(world);

        if (Resolve)
        {
            ResolvePairs(world);
        }

        Report(world);
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
            _masks.Remove(entity);
            _triggers.Remove(entity);
            _immovable.Remove(entity);
        }

        foreach (Entity entity in world.Enumerate<ColliderComponent>())
        {
            if (!world.Has<TransformComponent>(entity))
            {
                continue;
            }

            ColliderComponent collider = world.Get<ColliderComponent>(entity);
            Aabb box = BoxOf(world, entity, collider);

            _masks[entity] = collider.Mask == CollisionLayer.None ? CollisionLayer.Everything : collider.Mask;
            _triggers[entity] = collider.Trigger;
            _immovable[entity] = collider.Immovable;

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

    private static Aabb BoxOf(World world, Entity entity, in ColliderComponent collider)
    {
        TransformComponent transform = world.Get<TransformComponent>(entity);
        return Aabb.FromRect(new Rect(transform.Position + collider.Offset, collider.Size * transform.Scale));
    }

    /// <summary>Returns the box of an entity as it stands now, which a resolution that moved a body reads after every pair.</summary>
    private static Aabb CurrentBox(World world, Entity entity)
    {
        ColliderComponent collider = world.Get<ColliderComponent>(entity);
        return BoxOf(world, entity, collider);
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

    /// <summary>Finds the pairs of a step and keeps the ones the layers of the two colliders answer.</summary>
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

                    if (!_seen.Add((a, b)))
                    {
                        continue;
                    }

                    if (!Answers(world, a, b) || !Answers(world, b, a))
                    {
                        continue;
                    }

                    if (!_boxes[a].Intersects(_boxes[b]))
                    {
                        continue;
                    }

                    bool trigger = _triggers[a] || _triggers[b];
                    _pairs.Add(new CollisionPair(a, b, trigger));
                    _current.Add((a, b));
                    Attach(world, a, b);
                    Attach(world, b, a);
                }
            }
        }
    }

    /// <summary>Returns a value indicating whether the collider of the first entity answers the layer of the second.</summary>
    /// <remarks>
    /// A collider that was never given a layer is in <see cref="CollisionLayer.Default"/>, because a document that says
    /// nothing about the layer of a box asks for the box that a game of collisions had before layers existed. A mask that
    /// was never given is every layer, which is what the scan of the world reads when it fills
    /// <see cref="_masks"/> rather than answering a document.
    /// </remarks>
    private bool Answers(World world, Entity entity, Entity other)
    {
        if (!world.Has<ColliderComponent>(other))
        {
            return false;
        }

        CollisionLayer layer = world.Get<ColliderComponent>(other).Layer;

        if (layer == CollisionLayer.None)
        {
            layer = CollisionLayer.Default;
        }

        CollisionLayer mask = _masks.TryGetValue(entity, out CollisionLayer known) ? known : CollisionLayer.Everything;

        return (mask & layer) != CollisionLayer.None;
    }

    /// <summary>Moves the bodies of a solid pair out of each other, unless both stand still.</summary>
    /// <remarks>
    /// The penetration is measured from the boxes of this step rather than from the boxes of the moment the pair was
    /// built, so a body that was moved by a pair that came before this one is moved out of the pair it stands in now.
    /// The resolution runs a fixed number of times rather than while some body is still inside another one, because a
    /// room of three bodies that all press on each other has no place to put them: the last step reports the pair that
    /// the space could not be found for, and a game reads the overlap rather than a loop that never ends.
    /// </remarks>
    private void ResolvePairs(World world)
    {
        if (_pairs.Count == 0)
        {
            return;
        }

        for (var pass = 0; pass < ResolutionIterations; pass++)
        {
            var moved = false;

            foreach (CollisionPair pair in _pairs)
            {
                if (pair.IsTrigger)
                {
                    continue;
                }

                if (!world.IsAlive(pair.A) || !world.IsAlive(pair.B))
                {
                    continue;
                }

                if (!world.Has<ColliderComponent>(pair.A) || !world.Has<ColliderComponent>(pair.B))
                {
                    continue;
                }

                bool stillA = _immovable.TryGetValue(pair.A, out bool a) && a;
                bool stillB = _immovable.TryGetValue(pair.B, out bool b) && b;

                if (stillA && stillB)
                {
                    continue;
                }

                Aabb boxA = CurrentBox(world, pair.A);
                Aabb boxB = CurrentBox(world, pair.B);
                Vector2 penetration = boxA.Penetration(boxB);

                if (penetration == Vector2.Zero)
                {
                    continue;
                }

                // The body that stands still does not move, and the one that does takes the whole of the penetration;
                // two bodies that both move share it, which is what a person sees when two people walk into each other.
                if (stillA)
                {
                    Move(world, pair.B, -penetration);
                }
                else if (stillB)
                {
                    Move(world, pair.A, penetration);
                }
                else
                {
                    Move(world, pair.A, penetration * 0.5f);
                    Move(world, pair.B, -penetration * 0.5f);
                }

                moved = true;
            }

            if (!moved)
            {
                break;
            }
        }
    }

    /// <summary>Moves the transform of an entity and keeps the box of the step in step with it.</summary>
    private void Move(World world, Entity entity, Vector2 offset)
    {
        if (!world.Has<TransformComponent>(entity))
        {
            return;
        }

        world.GetRef<TransformComponent>(entity).Position += offset;

        if (_boxes.TryGetValue(entity, out Aabb box))
        {
            RemoveFromCells(entity, box);
            Aabb moved = box.MovedBy(offset);
            _boxes[entity] = moved;
            AddToCells(entity, moved);
        }
    }

    /// <summary>Compares the pairs of the step with the pairs of the step before and raises the three events for what changed.</summary>
    /// <remarks>
    /// The pairs are remembered as the two entities themselves rather than as the identifiers of their slots, so a slot
    /// that was handed out again names another entity than the one the contact was about: a pair of a destroyed entity
    /// and the entity that took its slot are two different pairs, and the one that is over is announced as it was rather
    /// than as the pair it became. A handler that reads an entity of an exit event reads the one the contact was about,
    /// which a world that no longer holds it reports as not alive, rather than a different entity that shares its slot.
    /// </remarks>
    private void Report(World world)
    {
        foreach (CollisionPair pair in _pairs)
        {
            if (!_previous.Contains((pair.A, pair.B)))
            {
                world.Events.Raise(new CollisionEnterEvent(pair.A, pair.B, pair.IsTrigger));
            }

            world.Events.Raise(new CollisionEvent(pair.A, pair.B, pair.IsTrigger));
        }

        foreach ((Entity a, Entity b) in _previous)
        {
            if (_current.Contains((a, b)))
            {
                continue;
            }

            world.Events.Raise(new CollisionExitEvent(a, b));
        }

        _previous.Clear();
        _previous.UnionWith(_current);
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
