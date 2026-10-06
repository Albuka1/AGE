using System.Diagnostics.CodeAnalysis;

namespace Age.Core;

/// <summary>
/// Stores resources of a single kind behind <see cref="ResourceHandle"/> values. The slot of a released resource is
/// reused with a new generation, so a stale handle never resolves to the resource that replaced it, and a resource can
/// be found again by the path it was registered under.
/// </summary>
public sealed class ResourcePool<T>
{
    private readonly int _owner = ResourceHandle.NextOwner();
    private Slot[] _slots = new Slot[4];
    private readonly Dictionary<string, ResourceHandle> _byPath = new(StringComparer.Ordinal);
    private int _count;

    /// <summary>Gets the number of live resources.</summary>
    public int Count => _count;

    /// <summary>
    /// Stores a resource and returns the handle that identifies it. Throws InvalidOperationException when the path is
    /// already registered, because the caller is expected to look it up with <see cref="TryGetHandle"/> instead.
    /// </summary>
    public ResourceHandle Add(T value, string? path = null)
    {
        if (path is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            if (_byPath.ContainsKey(path))
            {
                throw new InvalidOperationException($"A resource is already registered under '{path}'.");
            }
        }

        int id = FindFreeSlot();
        ref Slot slot = ref _slots[id - 1];
        slot.Value = value;
        slot.Path = path;
        slot.Generation++;
        slot.InUse = true;
        _count++;

        var handle = new ResourceHandle(_owner, id, slot.Generation);
        if (path is not null)
        {
            _byPath[path] = handle;
        }

        return handle;
    }

    /// <summary>
    /// Returns the live resource behind the handle. A handle whose slot was released and reused, an out of range
    /// handle and a default handle all return false.
    /// </summary>
    public bool TryGet(ResourceHandle handle, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetSlot(handle, out Slot slot))
        {
            value = slot.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Returns the handle that was registered under the given path.</summary>
    public bool TryGetHandle(string path, out ResourceHandle handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _byPath.TryGetValue(path, out handle);
    }

    /// <summary>Releases the resource behind the handle. Returns false when the handle is stale or already released.</summary>
    public bool Release(ResourceHandle handle)
    {
        if (!TryGetSlot(handle, out Slot slot))
        {
            return false;
        }

        if (slot.Path is not null)
        {
            _byPath.Remove(slot.Path);
        }

        ref Slot target = ref _slots[handle.Id - 1];
        target.Value = default!;
        target.Path = null;
        target.InUse = false;
        _count--;
        return true;
    }

    /// <summary>
    /// Releases every live resource, invoking <paramref name="release"/> for each value so that its owner can free the
    /// underlying device object. The slot is released before the callback runs, so a callback that throws still leaves
    /// the pool consistent. The generations of the slots survive, so handles from before the call stay invalid.
    /// </summary>
    public void Clear(Action<T>? release = null)
    {
        for (int index = 0; index < _slots.Length; index++)
        {
            ref Slot slot = ref _slots[index];
            if (!slot.InUse)
            {
                continue;
            }

            T value = slot.Value;
            if (slot.Path is not null)
            {
                _byPath.Remove(slot.Path);
            }

            slot.Value = default!;
            slot.Path = null;
            slot.InUse = false;
            _count--;

            if (release is not null)
            {
                release(value);
            }
        }
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
        public string? Path;
        public int Generation;
        public bool InUse;
    }
}
