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

    /// <summary>Creates an entity and returns its identifier. The slot of a destroyed entity is handed out again in a new generation.</summary>
    /// <remarks>
    /// The components of the entity that used the slot before were removed with it, and the storage of the slot is
    /// handed out here again: a reference that was obtained with <see cref="GetRef{T}"/> for the older entity and was
    /// held across its destruction points at the storage of this entity now.
    /// </remarks>
    public Entity CreateEntity()
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

        _alive[id] = true;
        return new Entity(id, _generations[id]);
    }

    /// <summary>Destroys an entity, removes every component attached to it and returns its slot to the pool.</summary>
    /// <remarks>
    /// The generation of the slot grows, so every identifier that named the entity before this call stops being alive,
    /// and destroying an entity twice does nothing. A reference that was obtained with <see cref="GetRef{T}"/> for one
    /// of its components is invalidated, even though it keeps pointing into the storage of the slot.
    /// </remarks>
    public void DestroyEntity(Entity entity)
    {
        if (!IsAlive(entity))
        {
            return;
        }

        _alive[entity.Id] = false;
        _generations[entity.Id]++;
        _free.Add(entity.Id);

        foreach (IComponentStore store in _stores.Values)
        {
            store.Remove(entity.Id);
        }
    }

    /// <summary>Determines whether an entity has been created and not yet destroyed, in the generation of its identifier.</summary>
    /// <remarks>A default identifier is never alive, and neither is one of a destroyed entity, even when the slot was handed out again afterwards.</remarks>
    public bool IsAlive(Entity entity) =>
        entity.Id >= 0 && entity.Id < _alive.Count && _alive[entity.Id] && _generations[entity.Id] == entity.Generation;

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
    /// <returns>An <see cref="IEnumerable{Entity}"/> over the entities. The sequence is produced lazily, so do not change the world while enumerating it.</returns>
    /// <remarks>
    /// Identifiers are handed out in ascending order and reused, so the slots are visited in the order in which they
    /// first appeared, not in the order in which the entities that hold them now were created.
    /// </remarks>
    public IEnumerable<Entity> Enumerate()
    {
        for (int id = 0; id < _alive.Count; id++)
        {
            if (_alive[id])
            {
                yield return new Entity(id, _generations[id]);
            }
        }
    }

    /// <summary>Returns the entities that currently have a component of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type to filter on.</typeparam>
    /// <returns>An <see cref="IEnumerable{Entity}"/> over the matching entities, ordered by ascending <see cref="Entity.Id"/>. The sequence is produced lazily, so do not change the world while enumerating it.</returns>
    public IEnumerable<Entity> Enumerate<T>() where T : struct, IComponent
    {
        if (!_stores.TryGetValue(typeof(T), out IComponentStore? existing))
        {
            yield break;
        }

        ComponentStore<T> store = (ComponentStore<T>)existing;
        for (int id = 0; id < store.Capacity; id++)
        {
            if (store.IsPresent(id))
            {
                yield return new Entity(id, _generations[id]);
            }
        }
    }

    /// <summary>Runs the pipeline against this world.</summary>
    public void Update(in GameTime time, SystemPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        pipeline.Update(this, time);
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

    private interface IComponentStore
    {
        bool Has(int id);

        void Remove(int id);
    }

    private sealed class ComponentStore<T> : IComponentStore where T : struct, IComponent
    {
        private T[] _data = new T[4];
        private bool[] _present = new bool[4];

        public int Capacity => _present.Length;

        public bool Has(int id) => id >= 0 && id < _present.Length && _present[id];

        public bool IsPresent(int id) => _present[id];

        public T Get(int id) => _data[id];

        public ref T GetRef(int id) => ref _data[id];

        public void Set(int id, in T value)
        {
            EnsureCapacity(id);
            _data[id] = value;
            _present[id] = true;
        }

        public void Remove(int id)
        {
            if (id < 0 || id >= _present.Length)
            {
                return;
            }

            _present[id] = false;
            _data[id] = default;
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
