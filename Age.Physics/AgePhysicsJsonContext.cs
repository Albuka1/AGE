using System.Text.Json.Serialization;

namespace Age.Physics;

/// <summary>
/// The source generated serialization contracts of the physics components.
/// </summary>
/// <remarks>
/// A context per assembly is what keeps the scene serializer free of reflection, so the components of this assembly can be
/// written to a scene and read from one in an AOT build as well.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
[JsonSerializable(typeof(ColliderComponent))]
internal sealed partial class AgePhysicsJsonContext : JsonSerializerContext
{
}
