using System.Diagnostics.CodeAnalysis;

namespace Age.Content.Prototypes;

/// <summary>
/// The prototypes of the content of a game, by the identifier a document declared them under.
/// </summary>
/// <remarks>
/// The manager is the one place that knows what the content holds, which is what lets a component refer to a weapon or an
/// enemy by name rather than carry a copy of it. A game reads its content once at startup and the manager refuses a
/// document that is broken, so a mistake in a file is a message at the start of the game rather than a surprise in the
/// middle of a fight.
/// </remarks>
/// <example>
/// <code>
/// var prototypes = new PrototypeManager(registry);
/// prototypes.Register("weapon", PrototypeReader.Weapon);
/// prototypes.Add("sword.yml", text);
/// prototypes.Build();
///
/// WeaponPrototype sword = prototypes.Get&lt;WeaponPrototype&gt;("Sword");
/// </code>
/// </example>
public interface IPrototypeManager
{
    /// <summary>Gets the identifiers of every prototype that was built, in the order the documents declared them.</summary>
    IEnumerable<string> Ids { get; }

    /// <summary>Gets the number of prototypes that were built.</summary>
    int Count { get; }

    /// <summary>Determines whether the content holds a prototype of that kind under that identifier.</summary>
    /// <typeparam name="T">The kind of the prototype.</typeparam>
    /// <param name="id">The identifier to look up.</param>
    /// <returns><see langword="true"/> when the content holds it.</returns>
    /// <exception cref="ArgumentNullException">The identifier is null.</exception>
    bool Has<T>(string id) where T : IPrototype;

    /// <summary>Returns a prototype of the content.</summary>
    /// <typeparam name="T">The kind of the prototype.</typeparam>
    /// <param name="id">The identifier to look up.</param>
    /// <returns>The prototype.</returns>
    /// <exception cref="ArgumentNullException">The identifier is null.</exception>
    /// <exception cref="KeyNotFoundException">The content holds no prototype of that kind under that identifier.</exception>
    T Get<T>(string id) where T : IPrototype;

    /// <summary>Returns a prototype of the content when it is there.</summary>
    /// <typeparam name="T">The kind of the prototype.</typeparam>
    /// <param name="id">The identifier to look up.</param>
    /// <param name="prototype">Receives the prototype when the content holds it.</param>
    /// <returns><see langword="true"/> when the content holds it.</returns>
    /// <exception cref="ArgumentNullException">The identifier is null.</exception>
    bool TryGet<T>(string id, [NotNullWhen(true)] out T? prototype) where T : IPrototype;

    /// <summary>Returns every prototype of one kind, in the order the documents declared them.</summary>
    /// <typeparam name="T">The kind of the prototype.</typeparam>
    /// <returns>The prototypes of that kind.</returns>
    IEnumerable<T> Enumerate<T>() where T : IPrototype;
}
