using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Age.Assets;
using Age.Content.Yaml;
using Age.Core;

namespace Age.Content.Prototypes;

/// <summary>
/// The default <see cref="IPrototypeManager"/>: it reads the documents of the content, resolves what they inherit from
/// each other, refuses what is broken, and hands out what is left.
/// </summary>
/// <remarks>
/// <para>
/// Reading and building are two steps on purpose. A document may inherit from a prototype that another file declares, so
/// nothing can be resolved before every file was read: <see cref="Add(string, string)"/> collects what a document says, and
/// <see cref="Build"/> resolves it, which is also the point where the mistakes of a whole content are reported rather
/// than one per start of a game.
/// </para>
/// <para>
/// The manager reads no file by itself: a caller hands it the text of a document and the name it came from, which is what
/// lets the same code load the content of a running game and check the content of a build.
/// </para>
/// </remarks>
public sealed class PrototypeManager : IPrototypeManager
{
    private readonly ComponentRegistry _components;
    private readonly Dictionary<string, Func<Prototype, IPrototype>> _kinds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Prototype> _declared = new(StringComparer.Ordinal);
    private readonly List<Prototype> _order = new();
    private readonly Dictionary<string, Prototype> _resolved = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IPrototype> _byId = new(StringComparer.Ordinal);
    private readonly List<IPrototype> _built = new();

    /// <summary>Initializes the manager with the registry that says which components the content may use.</summary>
    /// <param name="components">The registry of the components of the engine and the game.</param>
    /// <exception cref="ArgumentNullException">The registry is null.</exception>
    public PrototypeManager(ComponentRegistry components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
    }

    /// <inheritdoc />
    public IEnumerable<string> Ids => [.. _built.Select(prototype => prototype.Id)];

    /// <inheritdoc />
    public IEnumerable<Prototype> Prototypes =>
        [.. _order.Select(prototype => _resolved.TryGetValue(prototype.Id, out Prototype? resolved) ? resolved : prototype)];

    /// <inheritdoc />
    public int Count => _built.Count;

    /// <summary>Registers a kind of prototype, which is what the <c>type</c> field of a document names.</summary>
    /// <typeparam name="T">The data of that kind, which a game reads its content as.</typeparam>
    /// <param name="kind">The word that a document writes in its <c>type</c> field.</param>
    /// <param name="read">Reads the data of a prototype of that kind, which is where a content type is bound to a document.</param>
    /// <exception cref="ArgumentException">The kind is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The reader is null.</exception>
    /// <exception cref="InvalidOperationException">The kind is already registered.</exception>
    public void Register<T>(string kind, Func<Prototype, T> read) where T : IPrototype
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(read);

        if (_kinds.ContainsKey(kind))
        {
            throw new InvalidOperationException($"The kind '{kind}' of a prototype is already registered.");
        }

        _kinds[kind] = prototype => read(prototype);
    }

    /// <summary>Collects what one document says, which is what <see cref="Build"/> resolves afterwards.</summary>
    /// <param name="name">The name of the file the text came from, which an error mentions.</param>
    /// <param name="text">The text of the document.</param>
    /// <exception cref="ArgumentException">The name is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="PrototypeException">The text does not describe a prototype, and the error names the file and the line.</exception>
    /// <remarks>A document holds a list of prototypes, one after another, so that a folder of them reads like a catalogue.</remarks>
    public void Add(string name, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(text);

        YamlValue document;

        try
        {
            document = YamlReader.Read(text, name);
        }
        catch (YamlException exception)
        {
            // The reader knows the line and the column of the mistake and not the file it was reading, and a person who is
            // told to open a document has to be told which one: the name the text came from leaves with the error.
            throw new PrototypeException(exception.Message, name, exception.Line);
        }

        if (document is not YamlSequence sequence)
        {
            throw new PrototypeException($"{name}: a document of content holds a list of prototypes, and this one holds {Shape(document)}", name, document.Line);
        }

        foreach (YamlValue item in sequence.Items)
        {
            Add(name, item);
        }
    }

    /// <summary>Reads every document that a folder of the content holds, and builds what they declare.</summary>
    /// <param name="assets">The loader of the assets, which resolves the folder inside the game root.</param>
    /// <param name="folder">The folder to read, relative to the game root, such as <c>Prototypes</c>.</param>
    /// <returns>The number of prototypes that were built, which is zero when the folder holds nothing.</returns>
    /// <exception cref="ArgumentNullException">The loader is null.</exception>
    /// <exception cref="ArgumentException">The folder is null, empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The loader has not been initialized, or the folder escapes the game root.</exception>
    /// <exception cref="PrototypeException">A document is broken, and the error names the file and the line.</exception>
    /// <remarks>
    /// <para>
    /// Every file of the folder is read before any of them is resolved, because a prototype may inherit from one that
    /// another file declares, and the files are read in the order the loader returns them, so a content is read the same
    /// way everywhere. A file that is not a document of YAML is left alone, and a folder that is not there holds nothing
    /// rather than being an error.
    /// </para>
    /// <para>
    /// A manager is filled once, at startup, and this is the call that fills it: what the documents of the folder declare
    /// is added to the content the manager already holds, and then the whole content is built again. A second call
    /// therefore reads more files into the same content and refuses an identifier that any of them declares twice, so a
    /// game loads the folder of its content once and hands out what was built rather than reloading it.
    /// </para>
    /// </remarks>
    public int Load(IAssetLoader assets, string folder)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        IEnumerable<string> files;

        try
        {
            files = assets.Enumerate(folder);
        }
        catch (DirectoryNotFoundException)
        {
            // A game without content of its own is a game, so a folder that is not there holds nothing rather than being
            // an error: the loader has already named the folder it could not walk in its own record.
            return 0;
        }

        foreach (string file in files)
        {
            if (file.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                Add(file, assets.Load<string>(file));
            }
        }

        return Build();
    }

    /// <summary>Reads one prototype of a document, which is a set of fields with an identifier.</summary>
    private void Add(string name, YamlValue item)
    {
        if (item is not YamlMapping mapping)
        {
            throw new PrototypeException($"{name}: a prototype of a document is a set of fields, and this one is {Shape(item)}", name, item.Line);
        }

        string? id = null;
        string? kind = null;
        string? parent = null;
        string? nameText = null;
        string? descText = null;
        var components = new List<PrototypeComponent>();
        var fields = new List<PrototypeComponent>();

        foreach (YamlEntry entry in mapping.Entries)
        {
            switch (entry.Name)
            {
                case "id":
                    id = Word(name, entry);
                    break;

                case "type":
                    kind = Word(name, entry);
                    break;

                case "parent":
                    parent = Word(name, entry);
                    break;

                // A name and a description are the words a document writes for a thing that is not translated: a key of a language wins
                // over them where one is there, and a document that writes neither is named by its identifier, so nothing has to be
                // repeated in a file to say what an entity is called.
                case "name":
                    nameText = Word(name, entry);
                    break;

                case "desc":
                    descText = Word(name, entry);
                    break;

                case "components":
                    ReadComponents(name, entry, components);
                    break;

                // What is left is data of the kind rather than of the components of a thing: the stage, the values and the
                // uniforms of a material are fields of the document, and the kind that reads it is what says which of them it
                // knows. The manager carries them the way it carries a component, so a kind reads the document with the same
                // names, the file and the line.
                default:
                    fields.Add(new PrototypeComponent(entry.Name, YamlJson.Write(entry.Value), name, entry.Line));
                    break;
            }
        }

        if (id is null)
        {
            throw new PrototypeException($"{name}: a prototype says what it is known by, and this one has no 'id'", name, mapping.Line);
        }

        if (_declared.TryGetValue(id, out Prototype? declared))
        {
            throw new PrototypeException($"{name}: the identifier '{id}' was already declared in {declared.File}", name, mapping.Line);
        }

        var prototype = new Prototype(id, kind ?? "prototype", parent, null, null, name, mapping.Line, components, fields, nameText, descText);
        _declared[id] = prototype;
        _order.Add(prototype);
    }

    /// <summary>Reads the components of a prototype, which are the names of the components and the values they start with.</summary>
    private static void ReadComponents(string file, YamlEntry entry, List<PrototypeComponent> components)
    {
        if (entry.Value is not YamlSequence list)
        {
            throw new PrototypeException($"{file}: the components of a prototype are a list, and this one is {Shape(entry.Value)}", file, entry.Value.Line);
        }

        foreach (YamlValue item in list.Items)
        {
            if (item is not YamlMapping component)
            {
                throw new PrototypeException($"{file}: a component of a prototype is a set of fields that starts with its type, and this one is {Shape(item)}", file, item.Line);
            }

            string? componentName = null;
            var values = new List<YamlEntry>();

            foreach (YamlEntry field in component.Entries)
            {
                if (field.Name == "type")
                {
                    componentName = Word(file, field);
                    continue;
                }

                values.Add(field);
            }

            if (componentName is null)
            {
                throw new PrototypeException($"{file}: a component of a prototype says which component it is with a 'type'", file, component.Line);
            }

            int declared = components.FindIndex(candidate => string.Equals(candidate.Name, componentName, StringComparison.Ordinal));

            if (declared >= 0)
            {
                throw new PrototypeException($"{file}: the component '{componentName}' of this prototype is declared twice, and the first declaration is on line {components[declared].Line}", file, component.Line);
            }

            components.Add(new PrototypeComponent(componentName, YamlJson.Write(new YamlMapping(values, component.Line)), file, component.Line));
        }
    }

    /// <summary>Reads a field that holds one word.</summary>
    private static string Word(string file, YamlEntry entry) =>
        entry.Value is YamlScalar scalar && !scalar.IsEmpty
            ? scalar.Text
            : throw new PrototypeException($"{file}: the field '{entry.Name}' holds {Shape(entry.Value)} where one word is expected", file, entry.Value.Line);

    /// <summary>Names the shape of a value, which is what a message about a document says about what it found.</summary>
    private static string Shape(YamlValue value) => value switch
    {
        YamlScalar => "a word",
        YamlSequence => "a list",
        YamlMapping => "a set of fields",
        _ => "something this reader does not know",
    };

    /// <summary>Resolves what the documents inherit, refuses what is broken, and builds the prototypes of every kind.</summary>
    /// <returns>The number of prototypes that were built.</returns>
    /// <exception cref="PrototypeException">A document is broken, and the error names the file and the line.</exception>
    /// <remarks>
    /// The call is what a game makes once its content was read, and what a linter of the content makes in a build: every
    /// mistake of the whole content is reported here, before anything of the game runs.
    /// </remarks>
    public int Build()
    {
        _resolved.Clear();
        _byId.Clear();
        _built.Clear();

        foreach (Prototype declared in _order)
        {
            Prototype resolved = Resolve(declared, []);

            if (!_kinds.TryGetValue(resolved.Kind, out Func<Prototype, IPrototype>? read))
            {
                throw new PrototypeException($"{resolved.File}: the kind '{resolved.Kind}' of '{resolved.Id}' was not registered, so nothing in the game reads it", resolved.File, resolved.Line);
            }

            IPrototype instance = read(resolved);
            _byId[resolved.Id] = instance;
            _built.Add(instance);
        }

        return _built.Count;
    }

    /// <summary>Resolves the components of a prototype, which are its own and the ones it inherits.</summary>
    private Prototype Resolve(Prototype declared, HashSet<string> chain)
    {
        if (_resolved.TryGetValue(declared.Id, out Prototype? resolved))
        {
            return resolved;
        }

        if (!chain.Add(declared.Id))
        {
            throw new PrototypeException($"{declared.File}: the prototype '{declared.Id}' inherits from itself, through {string.Join(" then ", chain)}", declared.File, declared.Line);
        }

        IReadOnlyList<PrototypeComponent> components = declared.Components;
        IReadOnlyList<PrototypeComponent> fields = declared.Fields;
        string? nameText = declared.NameText;
        string? descText = declared.DescText;

        if (declared.Parent is string parentId)
        {
            if (!_declared.TryGetValue(parentId, out Prototype? parent))
            {
                throw new PrototypeException($"{declared.File}: the prototype '{declared.Id}' inherits from '{parentId}', and no document of the content declares it", declared.File, declared.Line);
            }

            Prototype inherited = Resolve(parent, chain);
            components = Merge(inherited.Components, declared.Components);
            fields = Merge(inherited.Fields, declared.Fields);

            // What names a thing is inherited the way a component is: a document that writes none takes the word of the kind
            // it inherits, so a name is written once where the kind is declared rather than once per thing.
            nameText ??= inherited.NameText;
            descText ??= inherited.DescText;
        }

        Validate(declared, components);

        var prototype = new Prototype(declared.Id, declared.Kind, declared.Parent, declared.NameKey, declared.DescKey, declared.File, declared.Line, components, fields, nameText, descText);
        _resolved[declared.Id] = prototype;
        return prototype;
    }

    /// <summary>Merges the components of a parent with the ones a prototype declares: a component of its own wins by name.</summary>
    private static List<PrototypeComponent> Merge(IReadOnlyList<PrototypeComponent> parent, IReadOnlyList<PrototypeComponent> own)
    {
        var merged = new List<PrototypeComponent>(parent);

        foreach (PrototypeComponent component in own)
        {
            int index = merged.FindIndex(candidate => string.Equals(candidate.Name, component.Name, StringComparison.Ordinal));

            if (index >= 0)
            {
                merged[index] = component;
                continue;
            }

            merged.Add(component);
        }

        return merged;
    }

    /// <summary>Refuses a component that the engine does not know and values that its contract cannot read.</summary>
    private void Validate(Prototype declared, IReadOnlyList<PrototypeComponent> components)
    {
        foreach (PrototypeComponent component in components)
        {
            if (!_components.Has(component.Name))
            {
                throw new PrototypeException($"{component.File}: the component '{component.Name}' of '{declared.Id}' is not registered, so nothing can read what it holds", component.File, component.Line);
            }

            try
            {
                _components.TryDeserialize(component.Name, component.Values, out _);
            }
            catch (JsonException exception)
            {
                throw new PrototypeException($"{component.File}: the values of the component '{component.Name}' of '{declared.Id}' cannot be read: {exception.Message}", component.File, component.Line);
            }
        }
    }

    /// <inheritdoc />
    public bool Has<T>(string id) where T : IPrototype => TryGet<T>(id, out _);

    /// <inheritdoc />
    public T Get<T>(string id) where T : IPrototype =>
        TryGet<T>(id, out T? prototype)
            ? prototype
            : throw new KeyNotFoundException($"The content holds no {typeof(T).Name} under the identifier '{id}'. Read the identifier from Ids, or check the content of the game.");

    /// <inheritdoc />
    public bool TryGet<T>(string id, [NotNullWhen(true)] out T? prototype) where T : IPrototype
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_byId.TryGetValue(id, out IPrototype? found) && found is T typed)
        {
            prototype = typed;
            return true;
        }

        prototype = default;
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<T> Enumerate<T>() where T : IPrototype => _built.OfType<T>();
}
