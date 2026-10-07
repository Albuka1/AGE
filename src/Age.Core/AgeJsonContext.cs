using System.Text.Json.Serialization;

namespace Age.Core;

/// <summary>
/// The source generated serialization contracts of the core components and of a scene.
/// </summary>
/// <remarks>
/// A source generated context keeps the scene serializer free of reflection, which is what lets it run in an AOT build.
/// Every assembly of the engine has a context for its own components, and a game writes one for the components it adds.
/// <c>IncludeFields</c> is set because the components hold their data in public fields.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
[JsonSerializable(typeof(SceneData))]
[JsonSerializable(typeof(TransformComponent))]
internal sealed partial class AgeJsonContext : JsonSerializerContext
{
}
