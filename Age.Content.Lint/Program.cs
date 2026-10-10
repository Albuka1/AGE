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

// Age.Content.Lint reads the content of a game the way a build does, which is the one check a build can make about content:
// a document that is broken, a component that nothing knows, a field a document may not write, and a path of a resource
// that the build does not ship. It exits with a non-zero code when anything is wrong, so a content mistake fails a build
// instead of being found in a fight.
//
// The tool registers the kinds the engine ships and the components of every assembly of the engine. A game whose content
// has kinds of its own reads the same linter from its own host, because a kind is what the game registers:
//
//     var linter = new ContentLinter(prototypes, registry, assets, images);
//     LintResult result = linter.Lint(new LintOptions { Root = "Resources", Prototypes = "Prototypes", Sheets = "Textures" });
//
// Every mistake is grouped under the pass that found it, which is what an editor and a person both want.
//
// Usage: dotnet run --project Age.Content.Lint -- [game root] [prototypes folder] [textures folder] [area]
//
// The area names one pass to read — prototypes, sheets or locales — and leaving it out reads every pass. A build that runs one pass
// per job names the pass, so a failure says which part of the content is wrong, and the four folders stay defaulted so the common
// call reads everything.
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

// A mistake is written under the pass that found it, so a person is told which part of the content to look at rather than being
// handed one list that mixes a document with a string of a language.
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
