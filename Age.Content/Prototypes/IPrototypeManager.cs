using System.Diagnostics.CodeAnalysis;
using Age.Assets;

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

    /// <summary>Gets the data of every prototype that was built, in the order the documents declared them.</summary>
    /// <remarks>
    /// A game reads its content through the kind it registered, and a tool that works on the content rather than on one kind
    /// of it reads this: it is what lets a linter check every document of a folder without knowing what a game reads them
    /// as. A component of a prototype carries the file and the line it came from, so a mistake has a place to name.
    /// </remarks>
    IEnumerable<Prototype> Prototypes { get; }

    /// <summary>Reads every document that a folder of the content holds, and builds what they declare.</summary>
    /// <param name="assets">The loader of the assets, which resolves the folder inside the game root.</param>
    /// <param name="folder">The folder to read, relative to the game root, such as <c>Prototypes</c>.</param>
    /// <returns>The number of prototypes that were built, which is zero when the folder holds nothing.</returns>
    /// <exception cref="PrototypeException">A document is broken, and the error names the file and the line.</exception>
    /// <remarks>
    /// This is the one call that fills a manager, and it is what a game makes once at startup: see
    /// <see cref="PrototypeManager.Load"/> for what it does with the manager it is called on.
    /// </remarks>
    int Load(IAssetLoader assets, string folder);

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
