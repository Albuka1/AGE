namespace Age.Core;

/// <summary>
/// Owns entities and their components. A world is not registered in the dependency injection container.
/// </summary>
public sealed class World
{
    private readonly Dictionary<int, bool> _alive = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();
    private int _nextId;

    /// <summary>Creates a new entity and returns its identifier. Identifiers are never reused.</summary>
    public Entity CreateEntity()
    {
        int id = _nextId;
        _nextId++;
        _alive[id] = true;
        return new Entity(id);
    }

    /// <summary>Destroys an entity and removes every component attached to it.</summary>
    public void DestroyEntity(Entity entity)
    {
        if (!IsAlive(entity))
        {
            return;
        }

        _alive[entity.Id] = false;

        foreach (IComponentStore store in _stores.Values)
        {
            store.Remove(entity.Id);
        }
    }

    /// <summary>Determines whether an entity has been created and not yet destroyed.</summary>
    public bool IsAlive(Entity entity) => _alive.TryGetValue(entity.Id, out bool alive) && alive;

    /// <summary>
    /// Returns the component of type <typeparamref name="T"/> attached to the entity.
    /// Throws InvalidOperationException if entity is not alive OR if component T is not present. Use Has&lt;T&gt; to check first.
    /// </summary>
    public T Get<T>(Entity entity) where T : struct, IComponent
    {
        ComponentStore<T> store = GetStore<T>();
        EnsurePresent(entity, store);
        return store.Get(entity.Id);
    }

    /// <summary>
    /// Returns a reference to the component of type <typeparamref name="T"/> attached to the entity.
    /// Storage uses Dictionary&lt;Type, Array&gt; per component type. Boxing not allowed; components must be struct.
    /// Returned ref is invalidated by subsequent Set&lt;T&gt; or CreateEntity calls for the same component type. Same throw semantics as Get.
    /// </summary>
    public ref T GetRef<T>(Entity entity) where T : struct, IComponent
    {
        ComponentStore<T> store = GetStore<T>();
        EnsurePresent(entity, store);
        return ref store.GetRef(entity.Id);
    }

    /// <summary>Attaches a component to the entity, replacing any existing value of the same type.</summary>
    public void Set<T>(Entity entity, in T value) where T : struct, IComponent
    {
        EnsureAlive(entity);
        GetStore<T>().Set(entity.Id, value);
    }

    /// <summary>Determines whether the entity has a component of type <typeparamref name="T"/>.</summary>
    public bool Has<T>(Entity entity) where T : struct, IComponent =>
        IsAlive(entity) && _stores.TryGetValue(typeof(T), out IComponentStore? store) && store.Has(entity.Id);

    /// <summary>Removes the component of type <typeparamref name="T"/> from the entity if it is present.</summary>
    public void Remove<T>(Entity entity) where T : struct, IComponent
    {
        if (_stores.TryGetValue(typeof(T), out IComponentStore? store))
        {
            store.Remove(entity.Id);
        }
    }

    /// <summary>Returns the entities that currently have a component of type <typeparamref name="T"/>, in ascending identifier order.</summary>
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
                yield return new Entity(id);
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
            throw new InvalidOperationException($"Entity {entity.Id} is not alive.");
        }
    }

    private void EnsurePresent<T>(Entity entity, ComponentStore<T> store) where T : struct, IComponent
    {
        EnsureAlive(entity);

        if (!store.Has(entity.Id))
        {
            throw new InvalidOperationException($"Entity {entity.Id} does not have a component of type {typeof(T).Name}.");
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
