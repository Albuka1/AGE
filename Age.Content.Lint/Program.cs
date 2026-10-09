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
//     LintReport report = linter.Lint("Prototypes");
//     LintReport sheets = linter.LintSheets("Textures");   // the grid of a sheet against its image, its version and its licence
//
// Usage: dotnet run --project Age.Content.Lint -- [game root] [prototypes folder] [textures folder]
string root = args.Length > 0 ? args[0] : "Resources";
string folder = args.Length > 1 ? args[1] : "Prototypes";
string textures = args.Length > 2 ? args[2] : "Textures";

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

IAssetLoader assets = provider.GetRequiredService<IAssetLoader>();
assets.Initialize(root);

var linter = new ContentLinter(prototypes, provider.GetRequiredService<ComponentRegistry>(), assets, provider.GetRequiredService<IImageLoader>());
LintReport report = linter.Lint(folder);
LintReport sheets = linter.LintSheets(textures);

foreach (LintProblem problem in report.Problems)
{
    Console.Error.WriteLine(problem);
}

foreach (LintProblem problem in sheets.Problems)
{
    Console.Error.WriteLine(problem);
}

if (!report.IsClean || !sheets.IsClean)
{
    Console.Error.WriteLine($"The content of '{root}' holds {report.Problems.Count + sheets.Problems.Count} mistakes.");
    return 1;
}

Console.WriteLine($"The content of '{root}' is sound: prototypes {report.Count}, sheets {sheets.Count}.");
return 0;
