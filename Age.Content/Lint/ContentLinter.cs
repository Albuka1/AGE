using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Age.Assets;
using Age.Content.Locale;
using Age.Content.Prototypes;
using Age.Content.Sheets;
using Age.Core;

namespace Age.Content.Lint;

/// <summary>
/// Checks the content of a game the way a build does: it reads every document of a folder and reports what is wrong with it,
/// rather than letting a game find out in the middle of a fight.
/// </summary>
/// <remarks>
/// <para>
/// Reading is what <see cref="IPrototypeManager"/> does, and the mistakes it refuses are the ones this reports first: a
/// document that is not a prototype, a component that nothing registered, values that the contract of a component cannot
/// read, a field that the format of a component does not carry — which is how a member that a scene leaves out, such as the
/// handle of a texture, is refused rather than dropped in silence — a parent that no file declares, a kind that nothing
/// reads, and a circle of inheritance. The mistake that no reader can see is the one this adds: a path that a
/// <see cref="ResourcePathAttribute"/> field names is a well-formed word whether or not the file behind it exists. The
/// reflection of <see cref="Check"/> is what still catches a field that a component does not carry when a registration was
/// written by hand, because the names of the format travel with the registrations that the source generator writes.
/// </para>
/// <para>
/// A linter is a tool rather than part of a game: it reads the types of the components with reflection, which an AOT build
/// trims away, and it runs at the moment a build is made.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var linter = new ContentLinter(prototypes, provider.GetRequiredService&lt;ComponentRegistry&gt;(), assets, images);
///
/// LintReport report = linter.Lint("Prototypes");
/// LintReport sheets = linter.LintSheets("Textures");
///
/// return report.IsClean &amp;&amp; sheets.IsClean ? 0 : 1;
/// </code>
/// </example>
public sealed class ContentLinter
{
    private const BindingFlags MembersOfAComponent = BindingFlags.Public | BindingFlags.Instance;

    private readonly IPrototypeManager _prototypes;
    private readonly ComponentRegistry _components;
    private readonly IAssetLoader _assets;
    private readonly IImageLoader _images;
    private readonly Dictionary<string, SpriteSheet?> _sheets = new(StringComparer.Ordinal);

    /// <summary>Initializes the linter with the content, the registry that names its components and the files of the game.</summary>
    /// <param name="prototypes">The content, which the linter reads.</param>
    /// <param name="components">The registry of the components of the engine and the game.</param>
    /// <param name="assets">The loader of the files, which answers whether a resource is there.</param>
    /// <param name="images">The loader of images, which reads the picture of a sheet to check it against the grid.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public ContentLinter(IPrototypeManager prototypes, ComponentRegistry components, IAssetLoader assets, IImageLoader images)
    {
        ArgumentNullException.ThrowIfNull(prototypes);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(images);

        _prototypes = prototypes;
        _components = components;
        _assets = assets;
        _images = images;
    }

    /// <summary>Reads every document of a folder and reports what is wrong with the content.</summary>
    /// <param name="folder">The folder to read, relative to the game root, such as <c>Prototypes</c>.</param>
    /// <returns>What was read and what is wrong with it, which holds no problem when the content is sound.</returns>
    /// <exception cref="ArgumentException">The folder is null, empty or whitespace.</exception>
    /// <remarks>
    /// The call does not throw for content that is broken: a mistake is what the report is for, so a build reads it rather
    /// than catching it. A mistake that stops the reading is the only problem of the report, because everything after it
    /// would be read out of content that was not understood.
    /// </remarks>
    public LintReport Lint(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        int count;

        try
        {
            count = _prototypes.Load(_assets, folder);
        }
        catch (PrototypeException exception)
        {
            return new LintReport(0, [new LintProblem(exception.File, exception.Line, exception.Message)]);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            // A folder that cannot be walked, a loader that was never initialized, or a path that escapes the game root:
            // nothing was read, and the message of the loader is what a person has to be told.
            return new LintReport(0, [new LintProblem(folder, 0, exception.Message)]);
        }

        var problems = new List<LintProblem>();

        foreach (Prototype prototype in _prototypes.Prototypes)
        {
            foreach (PrototypeComponent component in prototype.Components)
            {
                Check(component, prototype.Components, problems);
            }
        }

        return new LintReport(count, problems);
    }

    /// <summary>Reads every document of a sprite sheet of a folder and checks it against the image beside it.</summary>
    /// <param name="folder">The folder that holds the images and the documents beside them, relative to the game root.</param>
    /// <returns>What was read and what is wrong with it, which holds no problem when every sheet is sound.</returns>
    /// <exception cref="ArgumentException">The folder is null, empty or whitespace.</exception>
    /// <remarks>
    /// Every document of YAML in the folder is a sheet of the build, because the folder is the one that holds the images and
    /// the documents beside them: a document that is not a sheet is a mistake of where it lives, and a sheet that nothing
    /// reads is a sheet that nothing checks. What this checks is what nobody sees until the frame is drawn: the image that a
    /// document names and the grid it declares over it, the version of the format it is written in, and where its art comes
    /// from. The last two are rules of a build rather than of the format, which is why the reader leaves those fields alone.
    /// </remarks>
    public LintReport LintSheets(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        IEnumerable<string> files;

        try
        {
            files = _assets.Enumerate(folder);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            return new LintReport(0, [new LintProblem(folder, 0, exception.Message)]);
        }

        var problems = new List<LintProblem>();
        var count = 0;

        foreach (string file in files)
        {
            if (IsDocument(file) && Sheet(file, problems) is not null)
            {
                count++;
            }
        }

        return new LintReport(count, problems);
    }

    /// <summary>Reads one document of a sheet and checks what it says about itself and about its image.</summary>
    private SpriteSheet? Sheet(string file, List<LintProblem> problems)
    {
        SpriteSheet sheet;

        try
        {
            sheet = SpriteSheetReader.Read(_assets.Load<string>(file), file);
        }
        catch (SpriteSheetException exception)
        {
            problems.Add(new LintProblem(exception.File, exception.Line, exception.Message));
            return null;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            problems.Add(new LintProblem(file, 0, exception.Message));
            return null;
        }

        Attribution(file, sheet, problems);
        Grid(file, sheet, problems);

        return sheet;
    }

    /// <summary>Checks that a document says where its art comes from, which is what a build of a game has to be able to answer.</summary>
    private static void Attribution(string file, SpriteSheet sheet, List<LintProblem> problems)
    {
        if (sheet.License is null)
        {
            problems.Add(new LintProblem(file, 0, "the sheet does not say which licence its art comes with: every sheet declares one with 'license', and the art of this repository is MIT like the rest of it"));
        }

        if (sheet.Copyright is null)
        {
            problems.Add(new LintProblem(file, 0, "the sheet does not say who its art belongs to: every sheet declares that with 'copyright'"));
        }
    }

    /// <summary>Checks that the grid a document declares is the image it names, which is what keeps a frame from being cut in half.</summary>
    private void Grid(string file, SpriteSheet sheet, List<LintProblem> problems)
    {
        ImageData image;

        try
        {
            image = _images.Load(sheet.Image);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            problems.Add(new LintProblem(file, 0, $"the image of the sheet, '{sheet.Image}', cannot be read: {exception.Message}"));
            return;
        }

        int width = (int)(sheet.Columns * sheet.Cell.X);
        int height = (int)(sheet.Rows * sheet.Cell.Y);

        if (image.Width != width || image.Height != height)
        {
            problems.Add(new LintProblem(
                file,
                0,
                $"the sheet declares {sheet.Columns} by {sheet.Rows} cells of {sheet.Cell.X} by {sheet.Cell.Y} pixels, which is {width} by {height}, and '{sheet.Image}' is {image.Width} by {image.Height}"));
        }
    }

    /// <summary>Determines whether a file is a document of YAML, which is what a sheet is written in.</summary>
    private static bool IsDocument(string file) =>
        file.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads every document of every language and reports what is wrong with the strings of a game.</summary>
    /// <param name="folder">The folder that holds the languages, one folder each, relative to the game root, such as <c>Locale</c>.</param>
    /// <returns>What was read and what is wrong with it.</returns>
    /// <exception cref="ArgumentException">The folder is null, empty or whitespace.</exception>
    /// <remarks>
    /// What a build of a game can refuse that a frame cannot: a document that does not describe strings, a key that two
    /// documents of one language both write, a text that refers to a key the language does not hold, a translation that holds
    /// a key the base language does not, and a name or a description that a prototype points at and no string answers. The base
    /// language is what every other language falls back to, so a folder that holds no such language is a problem of the content
    /// rather than an empty report.
    /// </remarks>
    public LintReport LintLocales(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var problems = new List<LintProblem>();
        var languages = new Dictionary<string, Dictionary<string, LocaleString>>(StringComparer.Ordinal);

        IEnumerable<string> files;

        try
        {
            files = _assets.Enumerate(folder);
        }
        catch (DirectoryNotFoundException)
        {
            // A game whose content says nothing yet has no folder of languages, which is what a game with no sheets looks like.
            return new LintReport(0, []);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException)
        {
            return new LintReport(0, [new LintProblem(folder, 0, exception.Message)]);
        }

        foreach (string file in files)
        {
            if (!IsDocument(file))
            {
                continue;
            }

            string[] segments = file.Split('/');

            // A document of a language lives in the folder of that language under the folder of the languages, so a document
            // that is written anywhere else is a mistake of where it lives rather than a language of its own.
            if (segments.Length < 3)
            {
                problems.Add(new LintProblem(file, 0, "a document of a game lives in the folder of its language under the folder of the languages, such as Locale/en/Entities/creatures.yml"));
                continue;
            }

            string language = segments[1];

            if (!languages.TryGetValue(language, out Dictionary<string, LocaleString>? strings))
            {
                strings = new Dictionary<string, LocaleString>(StringComparer.Ordinal);
                languages[language] = strings;
            }

            try
            {
                foreach ((string key, LocaleString value) in LocaleReader.Read(_assets.Load<string>(file), file))
                {
                    if (strings.TryGetValue(key, out LocaleString? first))
                    {
                        problems.Add(new LintProblem(file, value.Line, $"the key '{key}' is already written by {first.File}, and a language says one thing per key"));
                        continue;
                    }

                    strings[key] = value;
                }
            }
            catch (LocaleException exception)
            {
                problems.Add(new LintProblem(exception.File, exception.Line, exception.Message));
            }
            catch (IOException exception)
            {
                problems.Add(new LintProblem(file, 0, exception.Message));
            }
        }

        return Strings(languages, problems);
    }

    /// <summary>Checks what the languages of a game say about each other, which is what a build can refuse.</summary>
    private LintReport Strings(Dictionary<string, Dictionary<string, LocaleString>> languages, List<LintProblem> problems)
    {
        var count = languages.Sum(language => language.Value.Count);

        if (!languages.TryGetValue(LocaleService.Base, out Dictionary<string, LocaleString>? baseStrings))
        {
            problems.Add(new LintProblem(
                LocaleService.Base,
                0,
                $"the strings of a game hold the language '{LocaleService.Base}', which every other language falls back to, and this game holds {string.Join(", ", languages.Keys.Order(StringComparer.Ordinal))}"));

            return new LintReport(count, problems);
        }

        var locale = new LocaleService(_assets);

        foreach ((string language, Dictionary<string, LocaleString> strings) in languages)
        {
            foreach ((string key, LocaleString value) in strings)
            {
                if (!string.Equals(language, LocaleService.Base, StringComparison.Ordinal) && !baseStrings.ContainsKey(key))
                {
                    problems.Add(new LintProblem(value.File, value.Line, $"the string '{key}' is one that '{LocaleService.Base}' does not hold, so nothing of the game asks for it"));
                }

                foreach (string reference in References(value))
                {
                    if (!locale.Has(reference, language))
                    {
                        problems.Add(new LintProblem(value.File, value.Line, $"the string '{key}' of '{language}' says what '{reference}' says, and no string of that language answers"));
                    }
                }
            }
        }

        foreach (Prototype prototype in _prototypes.Prototypes)
        {
            // The kind is what knows how an entity names itself, so the rule of a name lives in one place rather than in two.
            if (!_prototypes.TryGet(prototype.Id, out IPrototype? built) || built is not EntityPrototype entity)
            {
                continue;
            }

            foreach (string key in new[] { entity.NameKey, entity.DescKey })
            {
                // The rule of what a key holds lives in the service of the strings, so a name and a description written as
                // attributes of a key are answered here the way the game answers them.
                if (!locale.Has(key, LocaleService.Base))
                {
                    problems.Add(new LintProblem(
                        entity.Data.File,
                        entity.Data.Line,
                        $"the entity '{entity.Id}' is named by the string '{key}', and '{LocaleService.Base}' holds no string under that key"));
                }
            }
        }

        return new LintReport(count, problems);
    }

    /// <summary>Returns the keys that the texts of a string refer to, which are what its language has to answer as well.</summary>
    private static IEnumerable<string> References(LocaleString value)
    {
        var references = new List<string>();

        foreach (string? text in new[] { value.Text }.Concat(value.Attributes.Values).Concat(value.Forms.Values))
        {
            if (text is not null && LocaleReference.TryKey(text) is string reference)
            {
                references.Add(reference);
            }
        }

        return references;
    }

    /// <summary>Checks one component of a prototype: what a document may write, and what it names.</summary>
    /// <param name="component">The component to check.</param>
    /// <param name="components">The components of the document the component belongs to, which a member of it may point at.</param>
    /// <param name="problems">The report that what is wrong is added to.</param>
    private void Check(PrototypeComponent component, IReadOnlyList<PrototypeComponent> components, List<LintProblem> problems)
    {
        if (component.Values.ValueKind != JsonValueKind.Object || !_components.TryGetType(component.Name, out Type? type))
        {
            // A component that the reader does not know was refused before this point, and a value that is not a set of
            // fields is read by the contract of the component rather than by the fields of a document.
            return;
        }

        foreach (JsonProperty field in component.Values.EnumerateObject())
        {
            MemberInfo? member = Member(type, field.Name);

            if (member is null)
            {
                problems.Add(new LintProblem(
                    component.File,
                    component.Line,
                    $"the component '{component.Name}' has no member '{field.Name}', so the value is read and dropped; a document writes the members of {type.Name} that are data rather than state of a run"));

                continue;
            }

            CheckNestedResources(component, member, field.Value, problems);

            if (member.GetCustomAttribute<ResourcePathAttribute>() is null)
            {
                continue;
            }

            if (field.Value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new LintProblem(
                    component.File,
                    component.Line,
                    $"the field '{member.Name}' of the component '{component.Name}' holds the path of a resource, and this value is {field.Value.ValueKind}"));

                continue;
            }

            CheckResource(component, member, field.Value.GetString()!, problems);
        }

        CheckState(component, type, components, problems);
    }

    /// <summary>Checks the resource paths that a value holds inside the members of a struct, such as the stack of fonts of a style.</summary>
    /// <param name="component">The component that the value belongs to.</param>
    /// <param name="member">The member of the component that holds the value.</param>
    /// <param name="value">The value of that member in the document.</param>
    /// <param name="problems">The problems that are collected.</param>
    /// <param name="depth">How deep the walk already is, which bounds the types that hold one another.</param>
    /// <remarks>
    /// A path that a document writes inside a struct is checked the same way as one at the top of a component, so a font of a
    /// stack is refused by a build rather than reaching a frame. Only a type that holds a path somewhere is walked, which
    /// keeps the walk out of the types that are numbers, boxes and colors.
    /// </remarks>
    private void CheckNestedResources(PrototypeComponent component, MemberInfo member, JsonElement value, List<LintProblem> problems, int depth = 0)
    {
        Type type = MemberType(member);

        if (depth > 2 || !HoldsResourcePath(type))
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                CheckNestedResources(component, member, item, problems, depth + 1);
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty field in value.EnumerateObject())
        {
            if (Member(type, field.Name) is not MemberInfo nested)
            {
                continue;
            }

            if (nested.GetCustomAttribute<ResourcePathAttribute>() is null)
            {
                CheckNestedResources(component, nested, field.Value, problems, depth + 1);

                continue;
            }

            if (field.Value.ValueKind != JsonValueKind.String)
            {
                problems.Add(new LintProblem(
                    component.File,
                    component.Line,
                    $"the field '{nested.Name}' of '{member.Name}' of the component '{component.Name}' holds the path of a resource, and this value is {field.Value.ValueKind}"));

                continue;
            }

            CheckResource(component, nested, field.Value.GetString()!, problems);
        }
    }

    /// <summary>Returns a value indicating whether a type holds a member that names the path of a resource.</summary>
    /// <param name="type">The type to look through.</param>
    /// <param name="depth">How deep the walk already is.</param>
    /// <returns><see langword="true"/> when a member of the type, or of a type that a member holds, names the path of a resource.</returns>
    /// <remarks>A type outside the assemblies of the engine is not walked: what a game hands to the engine is not a document of the engine's own content.</remarks>
    private bool HoldsResourcePath(Type type, int depth = 0)
    {
        if (depth > 2 || type.IsPrimitive || type.IsEnum || type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            return type.GetElementType() is Type element && HoldsResourcePath(element, depth + 1);
        }

        if (type.Namespace is not string space || !space.StartsWith("Age.", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (MemberInfo member in Members(type))
        {
            if (member.GetCustomAttribute<ResourcePathAttribute>() is not null || HoldsResourcePath(MemberType(member), depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the type of a member of a component.</summary>
    /// <param name="member">The member to read the type of.</param>
    /// <returns>The type of the value of the member.</returns>
    private static Type MemberType(MemberInfo member) =>
        member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

    /// <summary>Checks that a state a component names is one the sheet it names declares, which is what a game would find out at the first frame instead.</summary>
    /// <remarks>
    /// A member that carries <see cref="SheetStateAttribute"/> says which member holds the path of the sheet, either of the
    /// same component or of another one of the document, and both values are read out of the document: a document that writes
    /// one of them and not the other has nothing to check here, and a sheet that cannot be read is what the pass over the
    /// sheets of a build reports.
    /// </remarks>
    private void CheckState(PrototypeComponent component, Type type, IReadOnlyList<PrototypeComponent> components, List<LintProblem> problems)
    {
        foreach (MemberInfo member in Members(type))
        {
            if (member.GetCustomAttribute<SheetStateAttribute>() is not SheetStateAttribute marked)
            {
                continue;
            }

            string? path = marked.Component is Type owner
                ? Value(components, owner, marked.Sheet)
                : Value(component, type, marked.Sheet);

            if (Value(component, member) is not string state || path is not string sheetPath)
            {
                continue;
            }

            if (SheetAt(sheetPath) is not SpriteSheet sheet || sheet.TryGetState(state, out SpriteSheetState? _))
            {
                continue;
            }

            problems.Add(new LintProblem(
                component.File,
                component.Line,
                $"the state '{state}' of the component '{component.Name}' is not one that the sheet '{sheetPath}' declares: it holds {string.Join(", ", sheet.States.Keys)}"));
        }
    }

    /// <summary>Returns the value that a document writes for a member of another component of it, or null when it writes none.</summary>
    private string? Value(IReadOnlyList<PrototypeComponent> components, Type component, string member)
    {
        foreach (PrototypeComponent sibling in components)
        {
            if (_components.TryGetType(sibling.Name, out Type? type) && type == component)
            {
                return Value(sibling, type, member);
            }
        }

        return null;
    }

    /// <summary>Returns the sheet of a document, reading it once, or null when it cannot be read.</summary>
    private SpriteSheet? SheetAt(string relativePath)
    {
        if (_sheets.TryGetValue(relativePath, out SpriteSheet? read))
        {
            return read;
        }

        SpriteSheet? sheet = null;

        try
        {
            sheet = SpriteSheetReader.Read(_assets.Load<string>(relativePath), relativePath);
        }
        catch (Exception exception) when (exception is IOException or SpriteSheetException or InvalidOperationException or ArgumentException)
        {
            // What is wrong with a document of a sheet is reported by the pass over the sheets of a build, which is where
            // every one of them is read: this only answers whether a state is one that the sheet declares.
            sheet = null;
        }

        _sheets[relativePath] = sheet;

        return sheet;
    }

    /// <summary>Returns the value that a document writes for a member of a component, or null when it writes none.</summary>
    private static string? Value(PrototypeComponent component, Type type, string member) =>
        Declared(type, member) is MemberInfo declared ? Value(component, declared) : null;

    /// <summary>Returns the value that a document writes for a member of a component, or null when it writes none.</summary>
    private static string? Value(PrototypeComponent component, MemberInfo member)
    {
        if (component.Values.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string name = member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? member.Name;

        foreach (JsonProperty field in component.Values.EnumerateObject())
        {
            if (!string.Equals(field.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            return field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() : null;
        }

        return null;
    }

    /// <summary>Checks that the file behind a path is one the build ships.</summary>
    private void CheckResource(PrototypeComponent component, MemberInfo member, string path, List<LintProblem> problems)
    {
        bool exists;

        try
        {
            exists = _assets.Exists(path);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            problems.Add(new LintProblem(component.File, component.Line, $"the path '{path}' of the component '{component.Name}' cannot be resolved: {exception.Message}"));
            return;
        }

        if (!exists)
        {
            problems.Add(new LintProblem(
                component.File,
                component.Line,
                $"the field '{member.Name}' of the component '{component.Name}' names '{path}', and no such file is in the game: a build would draw the placeholder instead"));
        }
    }

    /// <summary>Returns the member of a component type that a document writes under a name, or null when it writes none.</summary>
    /// <remarks>
    /// A member is a field, or a property with a public setter, which is what the source generated registrations carry and what
    /// the contract writes back: a document that writes a name the component carries is read, so a linter that looked at
    /// fields alone would report a document that the reading accepts. A member is written under its own name unless it carries
    /// one of its own, and a member that the format leaves out — the state of a run, such as the handle of a texture — is not
    /// one a document may write at all.
    /// </remarks>
    private static MemberInfo? Member(Type type, string name) =>
        Members(type).FirstOrDefault(member => Written(member, name));

    /// <summary>Returns the member of a component type that it declares under a name, or null when it declares none.</summary>
    private static MemberInfo? Declared(Type type, string name) =>
        Members(type).FirstOrDefault(member => string.Equals(member.Name, name, StringComparison.Ordinal));

    /// <summary>Enumerates what a document may write for a component: its fields, and its properties with a public setter.</summary>
    private static IEnumerable<MemberInfo> Members(Type type) =>
        type.GetFields(MembersOfAComponent)
            .Cast<MemberInfo>()
            .Concat(type.GetProperties(MembersOfAComponent).Where(property => property.SetMethod?.IsPublic is true && property.GetIndexParameters().Length == 0));

    /// <summary>Determines whether a document writes a member under a name, which is the name the member declares when it has one of its own.</summary>
    private static bool Written(MemberInfo member, string name) =>
        member.GetCustomAttribute<JsonIgnoreAttribute>() is null
        && string.Equals(member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? member.Name, name, StringComparison.Ordinal);
}
