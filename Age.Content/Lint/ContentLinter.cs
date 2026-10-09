using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Age.Assets;
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
/// return report.IsClean ? 0 : 1;
/// </code>
/// </example>
public sealed class ContentLinter
{
    private const BindingFlags FieldsOfAComponent = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>The extensions of the images that a sheet may name, which are the formats the loader of images reads.</summary>
    private static readonly string[] Images = [".png", ".bmp", ".tga", ".jpg", ".jpeg", ".gif"];

    private readonly IPrototypeManager _prototypes;
    private readonly ComponentRegistry _components;
    private readonly IAssetLoader _assets;
    private readonly IImageLoader _images;

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
                Check(component, problems);
            }
        }

        return new LintReport(count, problems);
    }

    /// <summary>Reads every document of a sprite sheet of a folder and checks it against the image beside it.</summary>
    /// <param name="folder">The folder that holds the images and the documents beside them, relative to the game root.</param>
    /// <returns>What was read and what is wrong with it, which holds no problem when every sheet is sound.</returns>
    /// <exception cref="ArgumentException">The folder is null, empty or whitespace.</exception>
    /// <remarks>
    /// A document is a sheet when an image of the same name stands beside it, which is how the folder is laid out: the
    /// sheets of a game are the documents under its textures. What this checks is what nobody sees until the frame is
    /// drawn: that the grid a document declares is the image it names, that it says which version of the format it is
    /// written in, and that it says where its art comes from. The last two are rules of a build rather than of the format,
    /// which is why the reader leaves those fields alone.
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
            if (IsDocument(file) && HasImageBeside(file) && Sheet(file, problems) is not null)
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

    /// <summary>Determines whether an image stands beside a document, which is what makes the document the sheet of it.</summary>
    private bool HasImageBeside(string file)
    {
        string name = file[..^Path.GetExtension(file).Length];

        foreach (string extension in Images)
        {
            if (_assets.Exists(name + extension))
            {
                return true;
            }
        }

        return false;
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
