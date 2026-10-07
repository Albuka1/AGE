namespace Age.Core;

/// <summary>
/// A checked borrow of a component. Every read and write validates the entity first, so the component of a slot that was
/// handed out again cannot be reached through an identifier that outlived its entity.
/// </summary>
/// <typeparam name="T">The component type. Components are structs that implement <see cref="IComponent"/>.</typeparam>
/// <remarks>
/// <para>
/// The type is a <c>ref struct</c>, so the compiler keeps a borrow on the stack: it cannot be stored in a field, in a
/// collection or in an asynchronous state machine, which is what keeps it from outliving the scope that took it.
/// </para>
/// <para>
/// <see cref="Value"/> and <see cref="Ref"/> run the same checks as <see cref="World.Get{T}"/> and
/// <see cref="World.Set{T}"/>, so a borrow of an entity that was destroyed since the borrow was taken throws instead of
/// reading or writing the component of the entity that holds the slot now.
/// </para>
/// <para>
/// Take a borrow where the entity can be destroyed between two statements. <see cref="World.GetRef{T}"/> hands out a raw
/// reference, which is what an inner loop wants, but nothing checks it after the call.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ComponentRef&lt;TransformComponent&gt; transform = world.Borrow&lt;TransformComponent&gt;(entity);
///
/// TransformComponent value = transform.Value;
/// value.Position += offset;
/// transform.Value = value;
/// </code>
/// </example>
public readonly ref struct ComponentRef<T> where T : struct, IComponent
{
    private readonly World _world;
    private readonly Entity _entity;

    /// <summary>Initializes a borrow. Only <see cref="World.Borrow{T}"/> creates borrows.</summary>
    internal ComponentRef(World world, Entity entity)
    {
        _world = world;
        _entity = entity;
    }

    /// <summary>Gets the entity that this borrow belongs to.</summary>
    public Entity Entity => _entity;

    /// <summary>Gets or sets a copy of the component.</summary>
    /// <remarks>
    /// The getter validates the entity and fails when it has no component of type <typeparamref name="T"/>. The setter
    /// validates the entity and then attaches the component, like <see cref="World.Set{T}"/>, so it may replace a
    /// component that was removed in the meantime.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The entity is not alive, or the getter found no component of type <typeparamref name="T"/>.</exception>
    public T Value
    {
        get => _world.Get<T>(_entity);
        set => _world.Set(_entity, value);
    }

    /// <summary>Gets a reference to the stored component, for a mutation in place.</summary>
    /// <exception cref="InvalidOperationException">The entity is not alive, or it has no component of type <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// The entity is validated before the reference is handed out, and the reference itself then has the lifetime of
    /// <see cref="World.GetRef{T}"/>: take it immediately before the write and do not hold it across a structural change.
    /// </remarks>
    public ref T Ref => ref _world.GetRef<T>(_entity);
}
