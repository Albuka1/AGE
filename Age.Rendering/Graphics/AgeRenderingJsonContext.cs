using System.Text.Json.Serialization;

namespace Age.Rendering;

/// <summary>
/// The source generated serialization contracts of the components of this assembly.
/// </summary>
/// <remarks>
/// A source generated context keeps the scene serializer free of reflection, which is what lets it run in an AOT build.
/// <c>IncludeFields</c> is set because the components hold their data in public fields.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(SpriteComponent))]
[JsonSerializable(typeof(TextComponent))]
[JsonSerializable(typeof(SpriteAnimationComponent))]
internal sealed partial class AgeRenderingJsonContext : JsonSerializerContext
{
}
