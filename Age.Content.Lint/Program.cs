using Age.Assets;
using Age.Audio;
using Age.Content;
using Age.Content.Lint;
using Age.Content.Prototypes;
using Age.Core;
using Age.Input;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using Microsoft.Extensions.DependencyInjection;

string root = args.Length > 0 ? args[0] : "Resources";
string folder = args.Length > 1 ? args[1] : "Prototypes";
string textures = args.Length > 2 ? args[2] : "Textures";
string? area = args.Length > 3 ? args[3] : null;

bool Read(LintArea candidate) => area is null || string.Equals(area, candidate.ToString(), StringComparison.OrdinalIgnoreCase);

if (area is not null && !Enum.GetNames<LintArea>().Any(name => string.Equals(name, area, StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine($"'{area}' is not a pass of the content: {string.Join(", ", Enum.GetNames<LintArea>())}.");
    return 1;
}

using ServiceProvider provider = new ServiceCollection()
    .AddAgeCore()
    .AddAgeContent()
    .AddAgeAssets()
    .AddAgeInput()
    .AddAgeAudio()
    .AddAgePhysics()
    .AddAgeUI()
    .AddAgeRendering()
    .BuildServiceProvider();

PrototypeManager prototypes = provider.GetRequiredService<PrototypeManager>();

// The tool reads the kinds the engine declares itself, because it does not link the assembly of a game: a game that lints its
// own content registers its kinds the same way, through AddAgeContentKinds, and the two kinds below are the two the engine
// declares on its own.
prototypes.Register(EntityPrototype.Kind, EntityPrototype.Read);
prototypes.Register(MaterialPrototype.Kind, MaterialPrototype.Read);

IAssetLoader assets = provider.GetRequiredService<IAssetLoader>();
assets.Initialize(root);

var linter = new ContentLinter(prototypes, provider.GetRequiredService<ComponentRegistry>(), assets, provider.GetRequiredService<IImageLoader>());

LintResult result = linter.Lint(new LintOptions
{
    Root = root,
    Prototypes = Read(LintArea.Prototypes) ? folder : null,
    Sheets = Read(LintArea.Sheets) ? textures : null,
    Locales = Read(LintArea.Locales) ? "Locale" : null,
});

foreach (LintReport report in result.Reports)
{
    foreach (LintProblem problem in report.Problems)
    {
        Console.Error.WriteLine($"[{report.Area}] {problem}");
    }
}

if (!result.IsClean)
{
    Console.Error.WriteLine($"The content of '{root}' holds {result.ProblemCount} mistakes.");
    return 1;
}

string passes = string.Join(", ", result.Reports.Select(report => $"{report.Area} {report.Count}"));
Console.WriteLine($"The content of '{root}' is sound: {passes}.");
return 0;
