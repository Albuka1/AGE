using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Age.Assets;
using Age.Content.Prototypes;
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
/// read, a field the component does not have, a parent that no file declares, a kind that nothing reads, and a circle of
/// inheritance. Two mistakes that a reader cannot see on its own are what this adds: a field that the contract refuses to
/// hold — state of a run rather than data of content, such as the handle of a texture — would be read silently and dropped,
/// and a path that names a file a build does not ship is a well-formed word either way.
/// </para>
/// <para>
/// A linter is a tool rather than part of a game: it reads the types of the components with reflection, which an AOT build
/// trims away, and it runs at the moment a build is made.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var linter = new ContentLinter(prototypes, provider.GetRequiredService&lt;ComponentRegistry&gt;(), assets);
///
/// LintReport report = linter.Lint("Prototypes");
///
/// return report.IsClean ? 0 : 1;
/// </code>
/// </example>
public sealed class ContentLinter
{
    private const BindingFlags FieldsOfAComponent = BindingFlags.Public | BindingFlags.Instance;

    private readonly IPrototypeManager _prototypes;
    private readonly ComponentRegistry _components;
    private readonly IAssetLoader _assets;

    /// <summary>Initializes the linter with the content, the registry that names its components and the files of the game.</summary>
    /// <param name="prototypes">The content, which the linter reads.</param>
    /// <param name="components">The registry of the components of the engine and the game.</param>
    /// <param name="assets">The loader of the files, which answers whether a resource is there.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
    public ContentLinter(IPrototypeManager prototypes, ComponentRegistry components, IAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(prototypes);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(assets);

        _prototypes = prototypes;
        _components = components;
        _assets = assets;
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
                Check(component, problems);
            }
        }

        return new LintReport(count, problems);
    }

    /// <summary>Checks one component of a prototype: what a document may write, and what it names.</summary>
    private void Check(PrototypeComponent component, List<LintProblem> problems)
    {
        if (component.Values.ValueKind != JsonValueKind.Object || !_components.TryGetType(component.Name, out Type? type))
        {
            // A component that the reader does not know was refused before this point, and a value that is not a set of
            // fields is read by the contract of the component rather than by the fields of a document.
            return;
        }

        foreach (JsonProperty field in component.Values.EnumerateObject())
        {
            FieldInfo? member = Member(type, field.Name);

            if (member is null)
            {
                problems.Add(new LintProblem(
                    component.File,
                    component.Line,
                    $"the component '{component.Name}' has no field '{field.Name}', so the value is read and dropped; a document writes the fields of {type.Name} that are data rather than state of a run"));

                continue;
            }

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
    }

    /// <summary>Checks that the file behind a path is one the build ships.</summary>
    private void CheckResource(PrototypeComponent component, FieldInfo member, string path, List<LintProblem> problems)
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

    /// <summary>Returns the field of a component type that a document writes under a name, or null when it writes none.</summary>
    /// <remarks>
    /// A field is what a document writes unless it carries a name of its own, and a field that the format leaves out — the
    /// state of a run, such as the handle of a texture — is not one a document may write at all.
    /// </remarks>
    private static FieldInfo? Member(Type type, string name) =>
        type.GetFields(FieldsOfAComponent).FirstOrDefault(field =>
            field.GetCustomAttribute<JsonIgnoreAttribute>() is null
            && string.Equals(field.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? field.Name, name, StringComparison.Ordinal));
}
