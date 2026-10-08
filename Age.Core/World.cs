namespace Age.Core;

/// <summary>
/// Owns entities and their components. A world is not registered in the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// A world is not thread-safe. Its storage is not synchronized and nothing serializes two calls that arrive at the same
/// time, so a world belongs to one thread: the one that runs the <see cref="IGameLoop"/> and calls the
/// <see cref="SystemPipeline"/>, which runs its systems in order on that thread. Calls that change the world come from
/// the update callback of the loop, and <see cref="Enumerate{T}"/> states the same rule for the sequence it hands out.
/// </para>
/// <para>
/// A game that touches a world from more than one thread serializes the calls itself, with a lock or a queue. The
/// generation checks of <see cref="IsAlive"/>, <see cref="GetRef{T}"/>, <see cref="Borrow{T}"/> and
/// <see cref="ComponentRef{T}"/> are correctness in that sequential use: they keep an identifier or a borrow that
/// outlived its entity from reaching the component of the entity that took the slot. They are not a concurrency
/// guarantee, because validation and access are two steps, so a destruction on another thread that lands between them
/// is a race that no check inside a single call can close.
/// </para>
/// </remarks>
public sealed class World
{
    private readonly List<bool> _alive = new();
    private readonly List<int> _generations = new();
    private readonly List<int> _free = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();
    private readonly List<PendingOperation> _pending = new();
    private readonly HashSet<Entity> _pendingDestroy = new();
    private readonly HashSet<int> _reserved = new();
    private int _structureVersion;

    /// <summary>Creates an entity and returns its identifier. The slot of a destroyed entity is handed out again in a new generation.</summary>
    /// <remarks>
    /// The components of the entity that used the slot before were removed with it, and the storage of the slot is
    /// handed out here again: a reference that was obtained with <see cref="GetRef{T}"/> for the older entity and was
    /// held across its destruction points at the storage of this entity now.
    /// </remarks>
    public Entity CreateEntity()
    {
        Entity entity = ReserveSlot();
        Activate(entity);
        return entity;
    }

    /// <summary>Destroys an entity, removes every component attached to it and returns its slot to the pool.</summary>
    /// <remarks>
    /// The generation of the slot grows, so every identifier that named the entity before this call stops being alive,
    /// and destroying an entity twice does nothing. A reference that was obtained with <see cref="GetRef{T}"/> for one
    /// of its components is invalidated, even though it keeps pointing into the storage of the slot. Calling this while
    /// the world is being enumerated throws: use <see cref="RequestDestroy"/> there, which defers the destruction to
    /// the end of the step.
    /// </remarks>
    public void DestroyEntity(Entity entity)
    {
        _pendingDestroy.Remove(entity);

        if (_reserved.Contains(entity.Id) && _generations[entity.Id] == entity.Generation)
        {
            // The entity was only reserved, so it never became part of the world: the slot goes back to the pool, its
            // generation grows so the cancelled identifier stays stale, and the creation that was queued for it is
            // refused by Activate.
            _reserved.Remove(entity.Id);
            _generations[entity.Id]++;
            _free.Add(entity.Id);
            return;
        }

        if (!IsAlive(entity))
        {
            return;
        }

        _alive[entity.Id] = false;
        _generations[entity.Id]++;
        _free.Add(entity.Id);
        _structureVersion++;

        foreach (IComponentStore store in _stores.Values)
        {
            store.Remove(entity.Id);
        }
    }

    /// <summary>Requests the creation of an entity, which is applied with the other pending operations.</summary>
    /// <returns>The identifier of the entity, which is not alive until the queue is applied.</returns>
    /// <remarks>
    /// This is how an entity is created while the world is being enumerated. The identifier is reserved right away, so
    /// the calls that follow it can already use it (<see cref="RequestSet{T}"/> on a reserved entity works), and the
    /// slot cannot be handed to another <see cref="CreateEntity"/> in the meantime. The entity itself exists only when
    /// <see cref="ApplyPending"/> runs: until then <see cref="IsAlive"/> reports it as absent and <see cref="Enumerate"/>
    /// does not visit it. Requesting the destruction of a reserved entity queues that destruction after the creation,
    /// so the two cancel out; destroying a reserved entity outright
    /// (<see cref="DestroyEntity"/>) cancels the creation, returns the slot to the pool and advances the generation of
    /// that slot, so the identifier that was reserved stays stale and a newer entity can take the slot safely.
    /// </remarks>
    public Entity RequestCreate()
    {
        Entity entity = ReserveSlot();
        _pending.Add(new CreateOperation(entity));
        return entity;
    }

    /// <summary>Requests the destruction of an entity, which is applied with the other pending operations.</summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <remarks>
    /// This is how an entity is removed while the world is being enumerated. From this call on the entity is not alive
    /// any more, so <see cref="IsAlive"/> and <see cref="Has{T}"/> already report it as gone for the systems of the
    /// same step, while its components stay in place until <see cref="ApplyPending"/> runs. Requesting the destruction
    /// of an entity that is already gone does nothing.
    /// </remarks>
    public void RequestDestroy(Entity entity)
    {
        // A reserved identifier is not alive yet, but its creation is already queued, so the destruction goes after it.
        // The slot has to carry the generation of this identifier: a stale one belongs to a reservation that was
        // cancelled, and the entity that holds the slot now is somebody else.
        bool reserved = _reserved.Contains(entity.Id) && _generations[entity.Id] == entity.Generation;

        if ((IsAlive(entity) || reserved) && _pendingDestroy.Add(entity))
        {
            _pending.Add(new DestroyOperation(entity));
        }
    }

    /// <summary>Requests that a component is attached to an entity, which is applied with the other pending operations.</summary>
    /// <typeparam name="T">The component type. Components are structs that implement <see cref="IComponent"/>.</typeparam>
    /// <param name="entity">The entity that receives the component.</param>
    /// <param name="value">The component to attach.</param>
    /// <remarks>
    /// A request for an entity that is destroyed before the pending operations run is dropped, so a sequence like
    /// «request the destruction, then request a write» does not throw.
    /// </remarks>
    public void RequestSet<T>(Entity entity, in T value) where T : struct, IComponent =>
        _pending.Add(new SetOperation<T>(entity, value));

    /// <summary>Requests that a component is removed from an entity, which is applied with the other pending operations.</summary>
    /// <typeparam name="T">The component type. Components are structs that implement <see cref="IComponent"/>.</typeparam>
    /// <param name="entity">The entity that loses the component.</param>
    public void RequestRemove<T>(Entity entity) where T : struct, IComponent =>
        _pending.Add(new RemoveOperation<T>(entity));

    /// <summary>Applies every operation that was requested since the last call, in the order it was requested.</summary>
    /// <returns>The number of operations that were applied, which is zero when none were pending.</returns>
    /// <remarks>
    /// <see cref="Update"/> and <see cref="UpdateFrame"/> call this before and after the systems of the step, so a game
    /// calls it itself only to flush requests made outside the loop, such as from a menu or a test. Operations that a
    /// request queues while this call runs are left for the next call.
    /// </remarks>
    public int ApplyPending()
    {
        if (_pending.Count == 0)
        {
            return 0;
        }

        PendingOperation[] batch = [.. _pending];
        _pending.Clear();

        foreach (PendingOperation operation in batch)
        {
            operation.Apply(this);
        }

        return batch.Length;
    }

    /// <summary>Determines whether an entity has been created and not yet destroyed, in the generation of its identifier.</summary>
    /// <remarks>
    /// A default identifier is never alive, and neither is one of a destroyed entity, even when the slot was handed out
    /// again afterwards. An entity whose destruction was requested but not applied yet is not alive either: the request
    /// is a promise that the entity goes away at the end of the step, so every system of that step already treats it as
    /// gone. See <see cref="RequestDestroy"/>.
    /// </remarks>
    public bool IsAlive(Entity entity) =>
        entity.Id >= 0 && entity.Id < _alive.Count && _alive[entity.Id] && _generations[entity.Id] == entity.Generation &&
        (_pendingDestroy.Count == 0 || !_pendingDestroy.Contains(entity));

    /// <summary>Returns the component of type <typeparamref name="T"/> attached to the entity.</summary>
    /// <typeparam name="T">The component type to read. Components are structs that implement <see cref="IComponent"/>.</typeparam>
    /// <param name="entity">The entity that owns the component.</param>
    /// <returns>A copy of the stored component. Use <see cref="GetRef{T}"/> to write to it in place.</returns>
    /// <exception cref="InvalidOperationException">The entity is not alive, or it has no component of type <typeparamref name="T"/>. Check with <see cref="Has{T}"/> first.</exception>
    public T Get<T>(Entity entity) where T : struct, IComponent
    {
        ComponentStore<T> store = GetStore<T>();
        EnsurePresent(entity, store);
        return store.Get(entity.Id);
    }

    /// <summary>Returns a reference to the component of type <typeparamref name="T"/> attached to the entity, so it can be written in place.</summary>
    /// <typeparam name="T">The component type to reference. Components are structs that implement <see cref="IComponent"/>, and storage is an <see cref="Array"/> per type, so nothing is boxed.</typeparam>
    /// <param name="entity">The entity that owns the component.</param>
    /// <returns>A reference to the stored component.</returns>
    /// <exception cref="InvalidOperationException">The entity is not alive, or it has no component of type <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// <para>
    /// A reference points into the storage of the component type, so every structural change to the entity invalidates
    /// it: a <c>Set&lt;T&gt;</c> that grows the storage, a <see cref="Remove{T}"/>, and <see cref="DestroyEntity"/>. Take the
    /// reference immediately before the write and do not hold it across such a call.
    /// </para>
    /// <para>
    /// Nothing validates the reference after this call, and <see cref="DestroyEntity"/> leaves the storage of the slot
    /// behind for the next <see cref="CreateEntity"/> to use, so a reference that is held across the destruction of its
    /// entity writes into the component of the entity that takes the slot afterwards. Take a <see cref="Borrow{T}"/>
    /// instead where that can happen.
    /// </para>
    /// </remarks>
    public ref T GetRef<T>(Entity entity) where T : struct, IComponent
    {
        ComponentStore<T> store = GetStore<T>();
        EnsurePresent(entity, store);
        return ref store.GetRef(entity.Id);
    }

    /// <summary>Returns a borrow of the component of type <typeparamref name="T"/> attached to the entity, which validates the entity on every access.</summary>
    /// <typeparam name="T">The component type to borrow. Components are structs that implement <see cref="IComponent"/>.</typeparam>
    /// <param name="entity">The entity that owns the component.</param>
    /// <returns>A borrow of the stored component. The entity is checked here as well as on every read and write.</returns>
    /// <exception cref="InvalidOperationException">The entity is not alive, or it has no component of type <typeparamref name="T"/>. Check with <see cref="Has{T}"/> first.</exception>
    /// <remarks>
    /// A borrow costs a generation check per access, which a raw reference from <see cref="GetRef{T}"/> does not have, so
    /// take one where the entity can be destroyed between the borrow and the write. A borrow of an entity that was
    /// destroyed in the meantime throws instead of reaching the component of the entity that holds the slot now. The
    /// check is for a world that one thread touches at a time: see the threading model on <see cref="World"/>.
    /// </remarks>
    public ComponentRef<T> Borrow<T>(Entity entity) where T : struct, IComponent
    {
        EnsurePresent(entity, GetStore<T>());
        return new ComponentRef<T>(this, entity);
    }

    /// <summary>Attaches a component to the entity, replacing any existing value of the same type.</summary>
    /// <remarks>A reference that was obtained with <see cref="GetRef{T}"/> is invalidated when the call grows the storage of the component type.</remarks>
    public void Set<T>(Entity entity, in T value) where T : struct, IComponent
    {
        EnsureAlive(entity);
        GetStore<T>().Set(entity.Id, value);
    }

    /// <summary>Determines whether the entity has a component of type <typeparamref name="T"/>.</summary>
    public bool Has<T>(Entity entity) where T : struct, IComponent =>
        IsAlive(entity) && _stores.TryGetValue(typeof(T), out IComponentStore? store) && store.Has(entity.Id);

    /// <summary>Removes the component of type <typeparamref name="T"/> from the entity if it is present.</summary>
    /// <remarks>
    /// Removing a component that the entity does not have does nothing. So does an identifier of a destroyed entity,
    /// even when the slot it names was handed out again: its components went with the entity, and the ones now stored in
    /// the slot belong to another entity.
    /// </remarks>
    public void Remove<T>(Entity entity) where T : struct, IComponent
    {
        if (IsAlive(entity) && _stores.TryGetValue(typeof(T), out IComponentStore? store))
        {
            store.Remove(entity.Id);
        }
    }

    /// <summary>Returns every entity that is alive, ordered by ascending <see cref="Entity.Id"/>.</summary>
    /// <returns>An <see cref="IEnumerable{Entity}"/> over the entities. The sequence is produced lazily.</returns>
    /// <exception cref="InvalidOperationException">The world changed while the sequence was being enumerated.</exception>
    /// <remarks>
    /// <para>
    /// Identifiers are handed out in ascending order and reused, so the slots are visited in the order in which they
    /// first appeared, not in the order in which the entities that hold them now were created.
    /// </para>
    /// <para>
    /// Creating or destroying an entity while this sequence is being enumerated throws, because the sequence walks the
    /// storage that those calls change. Collect the entities into a list first, or use <see cref="RequestCreate"/> and
    /// <see cref="RequestDestroy"/>, which defer the change to the end of the step.
    /// </para>
    /// <para>
    /// An entity that is only requested so far is not part of the sequence: an entity whose destruction is pending is
    /// not visited (it is gone for the rest of the step, see <see cref="IsAlive"/>), and neither is one whose creation
    /// is still queued.
    /// </para>
    /// </remarks>
    public IEnumerable<Entity> Enumerate()
    {
        int version = _structureVersion;

        for (int id = 0; id < _alive.Count; id++)
        {
            EnsureUnchanged(
                _structureVersion == version,
                "The world gained or lost an entity while it was being enumerated. Collect the entities into a list first, or use RequestCreate and RequestDestroy, which defer the change to the end of the step.");

            Entity entity = new(id, _generations[id]);

            if (IsAlive(entity))
            {
                yield return entity;
            }
        }
    }

    /// <summary>Returns the entities that currently have a component of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type to filter on.</typeparam>
    /// <returns>An <see cref="IEnumerable{Entity}"/> over the matching entities, ordered by ascending <see cref="Entity.Id"/>. The sequence is produced lazily.</returns>
    /// <exception cref="InvalidOperationException">A component of that type was added to or removed from an entity while the sequence was being enumerated.</exception>
    /// <remarks>
    /// A component that appears on an entity or disappears from one while this sequence is being enumerated throws,
    /// because the sequence walks the storage that those changes grow or shrink. Writing over a component that is
    /// already there does not. Collect the entities into a list first, or use <see cref="RequestSet{T}"/> and
    /// <see cref="RequestRemove{T}"/>, which defer the change to the end of the step. An entity whose destruction is
    /// pending is not visited, even though its components are still in place.
    /// </remarks>
    public IEnumerable<Entity> Enumerate<T>() where T : struct, IComponent
    {
        if (!_stores.TryGetValue(typeof(T), out IComponentStore? existing))
        {
            yield break;
        }

        ComponentStore<T> store = (ComponentStore<T>)existing;
        int version = store.Version;

        for (int id = 0; id < store.Capacity; id++)
        {
            EnsureUnchanged(
                store.Version == version,
                $"A component of type {typeof(T).Name} was added to or removed from an entity while the world was being enumerated. Collect the entities into a list first, or use RequestSet and RequestRemove, which defer the change to the end of the step.");

            if (store.IsPresent(id))
            {
                Entity entity = new(id, _generations[id]);

                if (IsAlive(entity))
                {
                    yield return entity;
                }
            }
        }
    }

    /// <summary>Runs the pipeline against this world, and applies the operations that were requested around the step.</summary>
    /// <remarks>
    /// The pending operations are applied before the systems run and again after them, so a request that a system made
    /// during the step takes effect before the next one. See <see cref="ApplyPending"/>.
    /// </remarks>
    public void Update(in GameTime time, SystemPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ApplyPending();
        pipeline.Update(this, time);
        ApplyPending();
    }

    /// <summary>Runs the frame systems of the pipeline against this world, once per frame.</summary>
    /// <remarks>
    /// A game calls this from the render callback of the loop, which runs once per frame whether or not the simulation
    /// advanced, so the interface and the overlays of a paused game keep working. <see cref="Update"/> is the fixed
    /// step, which the clock can stop with <see cref="FixedTimestep.Paused"/>. The pending operations are applied the
    /// same way here, so a request that a frame system made takes effect before the next frame.
    /// </remarks>
    public void UpdateFrame(in GameTime frame, SystemPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ApplyPending();
        pipeline.UpdateFrame(this, frame);
        ApplyPending();
    }

    private void EnsureAlive(Entity entity)
    {
        if (!IsAlive(entity))
        {
            throw new InvalidOperationException($"{entity} is not alive.");
        }
    }

    private void EnsurePresent<T>(Entity entity, ComponentStore<T> store) where T : struct, IComponent
    {
        EnsureAlive(entity);

        if (!store.Has(entity.Id))
        {
            throw new InvalidOperationException($"{entity} does not have a component of type {typeof(T).Name}.");
        }
    }

    private ComponentStore<T> GetStore<T>() where T : struct, IComponent
    {
        if (_stores.TryGetValue(typeof(T), out IComponentStore? existing))
        {
            return (ComponentStore<T>)existing;
        }

        var store = new ComponentStore<T>();
        _stores[typeof(T)] = store;
        return store;
    }

    /// <summary>Takes a slot for an entity, without making it part of the world yet.</summary>
    private Entity ReserveSlot()
    {
        int id;
        if (_free.Count > 0)
        {
            int last = _free.Count - 1;
            id = _free[last];
            _free.RemoveAt(last);
        }
        else
        {
            id = _generations.Count;
            _generations.Add(1);
            _alive.Add(false);
        }

        // Until Activate runs, the slot is handed out but is not part of the world, which is what keeps another
        // CreateEntity from taking it and lets a cancelled reservation drop it.
        _reserved.Add(id);
        return new Entity(id, _generations[id]);
    }
    /// <summary>Makes a reserved entity part of the world.</summary>
    private void Activate(Entity entity)
    {
        // The reservation belongs to this entity only if the slot still carries its generation and is reserved. The
        // generation is checked first, so a stale queued creation neither creates anything nor takes the reservation of
        // a newer entity that was handed the same slot after the older one was cancelled.
        if (_generations[entity.Id] != entity.Generation || !_reserved.Contains(entity.Id))
        {
            return;
        }

        _reserved.Remove(entity.Id);
        _alive[entity.Id] = true;
        _structureVersion++;
    }

    /// <summary>Throws when an enumeration that is walking the world notices that the world changed underneath it.</summary>
    private static void EnsureUnchanged(bool unchanged, string message)
    {
        if (!unchanged)
        {
            throw new InvalidOperationException(message);
        }
    }

    private interface IComponentStore
    {
        bool Has(int id);

        void Remove(int id);
    }

    /// <summary>One operation that a game requested while it was not allowed to change the world right away.</summary>
    private abstract class PendingOperation
    {
        public abstract void Apply(World world);
    }

    private sealed class CreateOperation : PendingOperation
    {
        private readonly Entity _entity;

        public CreateOperation(Entity entity) => _entity = entity;

        public override void Apply(World world) => world.Activate(_entity);
    }

    private sealed class DestroyOperation : PendingOperation
    {
        private readonly Entity _entity;

        public DestroyOperation(Entity entity) => _entity = entity;

        public override void Apply(World world) => world.DestroyEntity(_entity);
    }

    private sealed class SetOperation<T> : PendingOperation where T : struct, IComponent
    {
        private readonly Entity _entity;
        private readonly T _value;

        public SetOperation(Entity entity, in T value)
        {
            _entity = entity;
            _value = value;
        }

        public override void Apply(World world)
        {
            // The entity may have been destroyed by an earlier operation of the same batch, and that wins.
            if (world.IsAlive(_entity))
            {
                world.Set(_entity, _value);
            }
        }
    }

    private sealed class RemoveOperation<T> : PendingOperation where T : struct, IComponent
    {
        private readonly Entity _entity;

        public RemoveOperation(Entity entity) => _entity = entity;

        public override void Apply(World world) => world.Remove<T>(_entity);
    }

    private sealed class ComponentStore<T> : IComponentStore where T : struct, IComponent
    {
        private T[] _data = new T[4];
        private bool[] _present = new bool[4];

        public int Capacity => _present.Length;

        /// <summary>Gets the number of changes that added or removed a component, which is what an enumeration watches.</summary>
        public int Version { get; private set; }

        public bool Has(int id) => id >= 0 && id < _present.Length && _present[id];

        public bool IsPresent(int id) => _present[id];

        public T Get(int id) => _data[id];

        public ref T GetRef(int id) => ref _data[id];

        public void Set(int id, in T value)
        {
            EnsureCapacity(id);

            if (!_present[id])
            {
                // Only a component that appears changes what an enumeration walks: writing over one that is there does not.
                Version++;
            }

            _data[id] = value;
            _present[id] = true;
        }

        public void Remove(int id)
        {
            if (id < 0 || id >= _present.Length || !_present[id])
            {
                return;
            }

            _present[id] = false;
            _data[id] = default;
            Version++;
        }

        private void EnsureCapacity(int id)
        {
            if (id < _present.Length)
            {
                return;
            }

            int size = _present.Length;
            while (size <= id)
            {
                size *= 2;
            }

            Array.Resize(ref _data, size);
            Array.Resize(ref _present, size);
        }
    }
}
