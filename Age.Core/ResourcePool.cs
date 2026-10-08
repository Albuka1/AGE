using System.Diagnostics.CodeAnalysis;

namespace Age.Core;

/// <summary>
/// Stores resources of a single kind behind <see cref="ResourceHandle"/> values, with a key index for caching.
/// </summary>
/// <typeparam name="TKey">The type of the key a resource is registered under, such as a path or a tuple of what a cache is keyed by.</typeparam>
/// <typeparam name="T">The resource payload, such as a device identifier or a small descriptor.</typeparam>
/// <remarks>
/// <para>
/// A pool owns the bookkeeping, not the payload. <see cref="Add(T, TKey)"/> hands out a handle and <see cref="TryGet"/> resolves
/// one. <see cref="Release"/> drops the payload of a single slot and <see cref="Clear"/> drops the payloads of all of
/// them, but neither frees the underlying resource, and only <see cref="Clear"/> can run a callback, so the caller is
/// the one that has to delete the device object.
/// </para>
/// <para>
/// The slot of a released resource is reused with a new generation, so a handle left over from before the release stops
/// resolving instead of pointing at the resource that replaced it. Every handle also carries the token of the pool that
/// issued it, so a handle passed to the wrong pool is rejected.
/// </para>
/// <para>
/// Every method of the pool is safe to call from more than one thread: the slots, the key index and the counter are
/// guarded by a lock, and the token of the owner is taken atomically, so a background loader cannot corrupt the pool
/// while it resolves handles. The payloads are not synchronized, and neither are the device objects a caller creates
/// around them: creating and deleting those belongs to the thread that owns the device.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var textures = new ResourcePool&lt;string, uint&gt;();
///
/// ResourceHandle handle = textures.Add(deviceId, "art/player.png");
/// if (textures.TryGet(handle, out uint id))
/// {
///     // id is the live device texture
/// }
///
/// textures.Clear(id =&gt; DeleteDeviceTexture(id));
/// </code>
/// </example>
public sealed class ResourcePool<TKey, T>
    where TKey : notnull
{
    private readonly int _owner = ResourceHandle.NextOwner();
    private readonly object _gate = new();
    private Slot[] _slots = new Slot[4];
    private readonly Dictionary<TKey, ResourceHandle> _byKey = new();
    private int _count;

    /// <summary>Gets the number of live resources.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _count;
            }
        }
    }

    /// <summary>Stores a resource that no key names and returns the handle that identifies it.</summary>
    /// <param name="value">The resource payload, owned by the pool from now on.</param>
    /// <returns>A handle to the stored resource. Its <see cref="ResourceHandle.Generation"/> is new even when the slot was used before.</returns>
    public ResourceHandle Add(T value) => AddSlot(value, default!, keyed: false);

    /// <summary>Stores a resource under a key, so that the same resource is not stored twice.</summary>
    /// <param name="value">The resource payload, owned by the pool from now on.</param>
    /// <param name="key">The key to register the resource under, so it can be found again with <see cref="TryGetHandle"/>.</param>
    /// <returns>A handle to the stored resource. Its <see cref="ResourceHandle.Generation"/> is new even when the slot was used before.</returns>
    /// <exception cref="InvalidOperationException">A resource is already registered under the key; look it up with <see cref="TryGetHandle"/> instead of adding a second one.</exception>
    public ResourceHandle Add(T value, TKey key) => AddSlot(value, key, keyed: true);

    /// <summary>Stores a resource and returns the handle that identifies it. The caller holds the lock.</summary>
    private ResourceHandle AddSlot(T value, TKey key, bool keyed)
    {
        lock (_gate)
        {
            if (keyed && _byKey.ContainsKey(key))
            {
                throw new InvalidOperationException($"A resource is already registered under '{key}'.");
            }

            int id = FindFreeSlot();
            ref Slot slot = ref _slots[id - 1];
            slot.Value = value;
            slot.Key = key;
            slot.HasKey = keyed;
            slot.Generation++;
            slot.InUse = true;
            _count++;

            var handle = new ResourceHandle(_owner, id, slot.Generation);
            if (keyed)
            {
                _byKey[key] = handle;
            }

            return handle;
        }
    }

    /// <summary>Returns the live resource behind the handle.</summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="value">Receives the stored resource when the handle resolves.</param>
    /// <returns><see langword="true"/> when the handle still refers to a live resource of this pool.</returns>
    /// <remarks>
    /// A default handle, an out of range handle, a handle issued by another pool and a handle whose slot was released
    /// and reused all return <see langword="false"/>.
    /// </remarks>
    public bool TryGet(ResourceHandle handle, [MaybeNullWhen(false)] out T value)
    {
        lock (_gate)
        {
            if (TryGetSlot(handle, out Slot slot))
            {
                value = slot.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>Returns the handle that was registered under the given key, which is how a cache avoids loading the same resource twice.</summary>
    /// <param name="key">A key that a resource was registered under.</param>
    /// <param name="handle">Receives the handle when the key is known.</param>
    /// <returns><see langword="true"/> when the key is still registered.</returns>
    public bool TryGetHandle(TKey key, out ResourceHandle handle)
    {
        lock (_gate)
        {
            return _byKey.TryGetValue(key, out handle);
        }
    }

    /// <summary>Returns the handles of the resources that are live at this moment.</summary>
    /// <returns>A snapshot of the handles, in slot order. The pool does not change while the caller walks the snapshot, so a resource can be released between two calls.</returns>
    /// <remarks>
    /// Use it to release resources one by one, which is what lets a caller keep a resource whose cleanup failed: unlike
    /// <see cref="Clear"/>, which forgets a slot before its callback runs, a loop over this snapshot decides per
    /// resource whether the slot may be forgotten.
    /// </remarks>
    public ResourceHandle[] GetHandles()
    {
        lock (_gate)
        {
            if (_count == 0)
            {
                return [];
            }

            var handles = new ResourceHandle[_count];
            int next = 0;

            for (int index = 0; index < _slots.Length && next < handles.Length; index++)
            {
                ref Slot slot = ref _slots[index];
                if (slot.InUse)
                {
                    handles[next++] = new ResourceHandle(_owner, index + 1, slot.Generation);
                }
            }

            return handles;
        }
    }

    /// <summary>Releases the resource behind the handle and forgets its key, so the slot becomes available for reuse with a new generation.</summary>
    /// <param name="handle">The handle to release.</param>
    /// <returns><see langword="true"/> when a live resource was released, <see langword="false"/> when the handle was stale or already released.</returns>
    /// <remarks>The pool does not free anything itself: delete the underlying device object next to the call.</remarks>
    public bool Release(ResourceHandle handle)
    {
        lock (_gate)
        {
            if (!TryGetSlot(handle, out Slot slot))
            {
                return false;
            }

            Forget(handle.Id, slot.Key, slot.HasKey);
            return true;
        }
    }

    /// <summary>Releases every live resource, invoking an optional callback so its owner can free the device object.</summary>
    /// <param name="release">Called once for every live resource with the stored payload. Pass null when the payloads need no cleanup.</param>
    /// <remarks>
    /// <para>
    /// Each slot is released before its callback runs, so a callback that throws still leaves the pool consistent. Only
    /// the handles of the slots that were already cleared stop resolving; the slots that the call did not reach stay
    /// live and their handles keep working. The generations of the cleared slots survive, so an older handle never
    /// points at a resource that is added later.
    /// </para>
    /// <para>
    /// The bookkeeping of a slot happens under the lock and its callback runs after the lock was released, so a callback
    /// may be slow, and may call back into the pool, without holding the pool against other threads. The call walks a
    /// snapshot of the live handles, so a resource that another thread adds while it runs stays live.
    /// </para>
    /// </remarks>
    public void Clear(Action<T>? release = null)
    {
        if (release is null)
        {
            lock (_gate)
            {
                ClearLocked(null);
            }

            return;
        }

        foreach (ResourceHandle handle in GetHandles())
        {
            T value;

            lock (_gate)
            {
                if (!TryGetSlot(handle, out Slot slot))
                {
                    continue;
                }

                value = slot.Value;
                Forget(handle.Id, slot.Key, slot.HasKey);
            }

            release(value);
        }
    }

    /// <summary>Releases every live slot, running the callback that is given for each of them. The caller holds the lock.</summary>
    private void ClearLocked(Action<T>? release)
    {
        for (int index = 0; index < _slots.Length; index++)
        {
            ref Slot slot = ref _slots[index];
            if (!slot.InUse)
            {
                continue;
            }

            T value = slot.Value;
            Forget(index + 1, slot.Key, slot.HasKey);
            release?.Invoke(value);
        }
    }

    /// <summary>Marks the slot as free and forgets its key. The caller holds the lock.</summary>
    private void Forget(int id, TKey key, bool hasKey)
    {
        if (hasKey)
        {
            _byKey.Remove(key);
        }

        ref Slot slot = ref _slots[id - 1];
        slot.Value = default!;
        slot.Key = default!;
        slot.HasKey = false;
        slot.InUse = false;
        _count--;
    }

    private bool TryGetSlot(ResourceHandle handle, out Slot slot)
    {
        slot = default;

        if (handle.Owner != _owner)
        {
            return false;
        }

        int index = handle.Id - 1;
        if (handle.Id <= 0 || index >= _slots.Length)
        {
            return false;
        }

        ref Slot candidate = ref _slots[index];
        if (!candidate.InUse || candidate.Generation != handle.Generation)
        {
            return false;
        }

        slot = candidate;
        return true;
    }

    private int FindFreeSlot()
    {
        for (int index = 0; index < _slots.Length; index++)
        {
            if (!_slots[index].InUse)
            {
                return index + 1;
            }
        }

        int firstNewSlot = _slots.Length;
        Array.Resize(ref _slots, _slots.Length * 2);
        return firstNewSlot + 1;
    }

    private struct Slot
    {
        public T Value;
        public TKey Key;
        public bool HasKey;
        public int Generation;
        public bool InUse;
    }
}
