using Age.Content.Prototypes;

namespace Age.Rendering;

/// <summary>
/// The materials of the content: what a name a layer writes is drawn with, and where the mistake of a material that cannot be
/// drawn with is reported.
/// </summary>
/// <remarks>
/// <para>
/// A material is the one place that holds the stages of a program and the values its uniforms start with, which is why a layer
/// names it rather than a path: a sprite that names <c>pulse</c> is drawn with what the document of that material declares, and the
/// values that differ between two sprites are the ones the layers write themselves. Two layers that name one material are drawn
/// with one program, so a switch of a program happens once per sprite rather than once per layer.
/// </para>
/// <para>
/// Nothing is compiled before a frame draws a material, so a game reads its content before it opens a window and a stage that is
/// never drawn is never compiled. A material that cannot be drawn with is answered with a default handle, which draws the layer
/// with the program of the engine, and <see cref="Missing"/> holds its identifier so a game can say what went wrong rather than
/// leaving a person to read the log.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// IMaterialService materials = provider.GetRequiredService&lt;IMaterialService&gt;();
///
/// materials.Register(prototypes);
/// materials.Build();
///
/// foreach (string id in materials.Missing)
/// {
///     Console.WriteLine($"The material '{id}' of the content is not drawn with.");
/// }
/// </code>
/// </example>
public interface IMaterialService
{
    /// <summary>Gets the number of materials that were registered.</summary>
    int Count { get; }

    /// <summary>Gets the identifiers of the materials that a layer asked for and that cannot be drawn with, in the order they were asked for.</summary>
    /// <remarks>The list is what a build reads: a register of content is checked without a device, and the mistake of a material is a mistake of the content rather than of a frame.</remarks>
    IEnumerable<string> Missing { get; }

    /// <summary>Registers a material that a game read from its content.</summary>
    /// <param name="id">The identifier a layer names the material by.</param>
    /// <param name="prototype">The material as the document declared it.</param>
    /// <exception cref="ArgumentException">The identifier is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">The prototype is null.</exception>
    /// <exception cref="InvalidOperationException">The material is already registered, its values cannot be read, or a frame already drew with another material.</exception>
    void Register(string id, MaterialPrototype prototype);

    /// <summary>Registers every material of a content.</summary>
    /// <param name="prototypes">The content to read the materials of.</param>
    /// <exception cref="ArgumentNullException">The content is null.</exception>
    /// <remarks>A game calls this once with the content of its own after the content was built, so a material of a prototype is registered the same way as an entity.</remarks>
    void Register(IPrototypeManager prototypes);

    /// <summary>Prepares the service to draw the materials that were registered.</summary>
    /// <remarks>The call does not compile anything: it is where a game says that its content is read, and a material that is registered after it would change under a frame that drew with another one.</remarks>
    void Build();

    /// <summary>Returns the shader that draws a layer, compiling the stages of its material on the first call for that material.</summary>
    /// <param name="material">The material of the layer.</param>
    /// <returns>The handle of the shader, or a default handle when the layer names nothing or its material cannot be drawn with.</returns>
    /// <remarks>The stage of a layer that writes its own paths is compiled by the shader service under that pair of paths, so two layers that write the same pair are drawn in one draw call.</remarks>
    ShaderHandle Shader(Material material);

    /// <summary>Sends the values of a material to a renderer, which draws the quads that follow with them.</summary>
    /// <param name="renderer">The renderer that is drawing.</param>
    /// <param name="shader">The shader that the values belong to, which is what a layer was handed by <see cref="Shader"/>.</param>
    /// <param name="material">The material of the layer, which is the order a document wrote.</param>
    /// <exception cref="ArgumentNullException">The renderer is null.</exception>
    /// <remarks>A value of a layer is sent after the one of the material it names, and a value that cannot be read is reported in the log and left out.</remarks>
    void SetShader(IRenderer renderer, ShaderHandle shader, Material material);

    /// <summary>Sends values that a game wrote itself to a renderer, which draws the quads that follow with them.</summary>
    /// <param name="renderer">The renderer that is drawing.</param>
    /// <param name="shader">The shader that the values belong to.</param>
    /// <param name="uniforms">The values to send, in the order they are sent.</param>
    /// <exception cref="ArgumentNullException">The renderer is null.</exception>
    /// <remarks>This is what a pass of a game uses: a shader of the content is compiled by <see cref="Shader"/> and the values a frame computes are sent with this.</remarks>
    void SetShader(IRenderer renderer, ShaderHandle shader, ReadOnlySpan<UniformValue> uniforms);

    /// <summary>
    /// One value of a material, which a document writes as the kind of a uniform and the numbers behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value belongs to one layer rather than to the program, and a document writes it the way GLSL names its type, so a value
    /// that a stage reads as a float and one it reads as an int are told apart and the renderer is handed one or the other. A
    /// material that names no value keeps the one the stage was written with, and a name that the stage does not declare is
    /// ignored by the device, which is what a uniform of one stage and not another looks like in practice.
    /// </para>
    /// <para>
    /// The kind is on a line of its own rather than in the flow style, because the flow style is not part of the subset of YAML
    /// that the content of the engine is read with:
    /// </para>
    /// <code>
    /// Uniforms:
    ///   Speed:
    ///     float: 4.0
    ///   Steps:
    ///     int: 8
    /// </code>
    /// </remarks>
    /// <param name="Name">The name of the uniform, as the stage declares it.</param>
    /// <param name="Kind">The kind the value was read as, which is what a renderer is handed it by.</param>
    /// <param name="Numbers">The numbers of the value, in the order the document wrote them.</param>
    public readonly record struct UniformValue(string Name, UniformKind Kind, ReadOnlyMemory<float> Numbers);

    /// <summary>
    /// The kind of a uniform of a material, which is the type a stage declares it as.
    /// </summary>
    /// <remarks>
    /// The kind is what tells a whole number from a number, because a document writes both as digits and only the document knows
    /// which one it meant: a value of the kind <see cref="Int"/> is set on a renderer as an <c>int</c> and one of every other kind
    /// as a <c>float</c>. A <see cref="Color"/> is the four numbers of a colour, written the way every colour of the content is
    /// written, in bytes, and sent as the four channels between zero and one that a stage draws with.
    /// </remarks>
    public enum UniformKind
    {
        /// <summary>One number, which a stage declares as <c>float</c>.</summary>
        Float,

        /// <summary>One whole number, which a stage declares as <c>int</c>.</summary>
        Int,

        /// <summary>Two numbers, which a stage declares as <c>vec2</c>.</summary>
        Vec2,

        /// <summary>Three numbers, which a stage declares as <c>vec3</c>.</summary>
        Vec3,

        /// <summary>Four numbers, which a stage declares as <c>vec4</c>.</summary>
        Vec4,

        /// <summary>Four numbers that a document wrote in bytes, which a stage declares as <c>vec4</c> and reads as a colour.</summary>
        Color,
    }
}
