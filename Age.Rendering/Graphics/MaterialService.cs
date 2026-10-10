using System.Globalization;
using System.Text.Json;
using Age.Content.Prototypes;
using Age.Core;
using Microsoft.Extensions.Logging;

namespace Age.Rendering;

/// <summary>
/// The default <see cref="IMaterialService"/>: it reads the materials of the content, compiles the stage of each of them through
/// <see cref="IShaderService"/>, and hands out the one shader that a name is drawn with.
/// </summary>
/// <remarks>
/// <para>
/// A material is <see cref="Material"/> of the content: its stages are read once with reflection, because the values of a uniform
/// are text in a document that only the type of the field can read, and its program is compiled on the first call rather than at
/// startup, which is what lets a game that loads its content before it has a window still draw. A program that cannot be compiled
/// or a value that cannot be read is refused with a message that names the file, the line and the uniform, and the material is
/// reported once: a frame after that draws with the material that was refused left out rather than with a program that a stage of
/// it was not read into.
/// </para>
/// <para>
/// A material is read with reflection through <see cref="ComponentRegistry"/>, so the same contract that reads a scene reads the
/// values of the uniforms of a material, and <c>Age.Content.Lint</c> can check a material without a device at all.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// IMaterialService materials = provider.GetRequiredService&lt;IMaterialService&gt;();
///
/// materials.Register("pulse", prototypes.Get&lt;MaterialPrototype&gt;("Pulse"));
/// materials.Build();
///
/// ShaderHandle shader = materials.Shader("pulse");
/// </code>
/// </example>
public sealed class MaterialService : IMaterialService
{
    private readonly IShaderService _shaders;
    private readonly ILogger<MaterialService>? _logger;
    private readonly Dictionary<string, Declared> _declared = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MaterialProgram> _programs = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Fragment, string Vertex), ShaderHandle> _inline = new();
    private readonly HashSet<string> _broken = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _failed = new(StringComparer.Ordinal);
    private readonly List<string> _missing = [];

    /// <summary>Initializes the service with the shaders it compiles through.</summary>
    /// <param name="shaders">The service that compiles the stages of a shader.</param>
    /// <param name="logger">The logger that reports a material that cannot be drawn with, or null to report nothing.</param>
    /// <exception cref="ArgumentNullException">The shader service is null.</exception>
    /// <remarks>The service is registered after the rendering of a game, which is what compiles a stage: a game that reads its content before it opens a window still reads its materials here, because nothing is compiled until a frame draws one.</remarks>
    public MaterialService(IShaderService shaders, ILogger<MaterialService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(shaders);
        _shaders = shaders;
        _logger = logger;
    }

    /// <inheritdoc />
    public int Count => _declared.Count;

    /// <inheritdoc />
    public IEnumerable<string> Missing => _missing;

    /// <summary>Determines whether a material is registered.</summary>
    /// <param name="id">The identifier a layer names the material by.</param>
    /// <returns><see langword="true"/> when the identifier is registered.</returns>
    /// <remarks>This is what a reader of content asks before it registers the material of a layer, so a material that two prototypes share is read once.</remarks>
    public bool IsRegistered(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _declared.ContainsKey(id);
    }

    /// <inheritdoc />
    public void Register(string id, MaterialPrototype prototype)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(prototype);

        if (_programs.Count > 0)
        {
            throw new InvalidOperationException($"The material '{id}' cannot be registered after another one was read: a material that a frame already drew with would change under it.");
        }

        var declared = new Declared(prototype.Fragment, prototype.Vertex, Values(id, prototype.Values));

        if (!_declared.TryAdd(id, declared))
        {
            throw new InvalidOperationException($"The material '{id}' was already registered.");
        }
    }

    /// <summary>Reads the values of a material into the kinds and the numbers that a renderer is handed.</summary>
    /// <param name="id">The identifier of the material, which a refusal names.</param>
    /// <param name="values">The values as the document wrote them.</param>
    /// <returns>The values, in the order the document wrote them.</returns>
    /// <exception cref="InvalidOperationException">A value is not one that its kind holds.</exception>
    /// <remarks>
    /// The values are read once, where the content is read, rather than on every frame that draws the material: a document that
    /// says a number where a colour belongs is a mistake of the content, so it is refused here, with the identifier of the
    /// material and of the uniform, rather than quieting a frame and being found much later. A colour is written the way every
    /// colour of the content is written, in bytes, and is read as the four channels between zero and one that a stage draws with.
    /// </remarks>
    private static IMaterialService.UniformValue[] Values(string id, JsonElement values)
    {
        if (values.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var read = new List<IMaterialService.UniformValue>();

        foreach (JsonProperty uniform in values.EnumerateObject())
        {
            if (uniform.Value.ValueKind != JsonValueKind.Object || uniform.Value.EnumerateObject().Count() != 1)
            {
                throw new InvalidOperationException($"The uniform '{uniform.Name}' of the material '{id}' is written as one kind and the value behind it, such as 'float: 4.0'.");
            }

            foreach (JsonProperty kind in uniform.Value.EnumerateObject())
            {
                read.Add(Value(id, uniform.Name, kind));
            }
        }

        return [.. read];
    }

    /// <summary>Reads one value of a material, which is one kind of a uniform and the numbers behind it.</summary>
    private static IMaterialService.UniformValue Value(string id, string uniform, JsonProperty kind)
    {
        string name = kind.Name.ToLowerInvariant();
        IMaterialService.UniformKind read = name switch
        {
            "float" => IMaterialService.UniformKind.Float,
            "int" => IMaterialService.UniformKind.Int,
            "vec2" => IMaterialService.UniformKind.Vec2,
            "vec3" => IMaterialService.UniformKind.Vec3,
            "vec4" => IMaterialService.UniformKind.Vec4,
            "color" => IMaterialService.UniformKind.Color,
            _ => throw new InvalidOperationException($"The uniform '{uniform}' of the material '{id}' is of the kind '{kind.Name}', and a material sends a float, an int, a vec2, a vec3, a vec4 or a color."),
        };

        int count = read switch
        {
            IMaterialService.UniformKind.Vec2 => 2,
            IMaterialService.UniformKind.Vec3 => 3,
            IMaterialService.UniformKind.Vec4 or IMaterialService.UniformKind.Color => 4,
            _ => 1,
        };

        float[] numbers = Numbers(id, uniform, kind.Value, count);

        if (read == IMaterialService.UniformKind.Color)
        {
            // A colour of the content is written the way a colour of a scene is, a byte for every channel, and a stage reads the
            // four channels between zero and one: the division happens here rather than in every frame that sends the value.
            for (var index = 0; index < numbers.Length; index++)
            {
                numbers[index] /= 255f;
            }
        }

        return new IMaterialService.UniformValue(uniform, read, numbers);
    }

    /// <summary>Reads the numbers of a value, which are the ones a renderer is handed.</summary>
    private static float[] Numbers(string id, string uniform, JsonElement value, int count)
    {
        if (count == 1)
        {
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.String && float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float single))
            {
                return [single];
            }

            throw new InvalidOperationException($"The uniform '{uniform}' of the material '{id}' holds {value.ValueKind} where one number belongs.");
        }

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != count)
        {
            throw new InvalidOperationException($"The uniform '{uniform}' of the material '{id}' holds {value.ValueKind} where {count} numbers belong.");
        }

        var numbers = new float[count];

        for (var index = 0; index < count; index++)
        {
            numbers[index] = value[index].GetSingle();
        }

        return numbers;
    }

    /// <inheritdoc />
    public void Register(IPrototypeManager prototypes)
    {
        ArgumentNullException.ThrowIfNull(prototypes);

        foreach (MaterialPrototype prototype in prototypes.Enumerate<MaterialPrototype>())
        {
            Register(prototype.Id, prototype);
        }
    }

    /// <inheritdoc />
    public void Build()
    {
        // Nothing is compiled here on purpose: a material is compiled the first time a frame draws it, so a game may read its
        // content before it has a window. What the call does is what a second registration after a frame would break.
        _programs.Clear();
        _inline.Clear();
        _broken.Clear();
        _failed.Clear();
        _missing.Clear();
    }

    /// <inheritdoc />
    ShaderHandle IMaterialService.Shader(Material material)
    {
        // A layer that writes the path of a stage is drawn by it, whatever else it names: the stage is the frame of a program, and
        // a game that shades one sprite differently writes the stage rather than the values of the one it shares. The pair of paths
        // is what the shader service keeps a program under, so two layers that write the same pair draw in one call, and a stage
        // that is not there is reported once rather than asked for on every frame.
        if (material.Fragment is string own)
        {
            var key = (Fragment: own, Vertex: material.Vertex ?? string.Empty);

            if (_inline.TryGetValue(key, out ShaderHandle held))
            {
                return held;
            }

            try
            {
                ShaderHandle handle = _shaders.Load(own, material.Vertex);
                _inline[key] = handle;
                return handle;
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or InvalidOperationException or ObjectDisposedException or FormatException or ArgumentException)
            {
                // The stage is asked for once, and the layer that names it is drawn with the program of the engine from then on:
                // a mistake of the content is reported where it is found rather than on every frame that draws the layer.
                _inline[key] = default;
                _broken.Add(own);
                _logger?.LogError("The stage '{Stage}' of a layer cannot be loaded, so the layer is drawn with the program of the engine: {Reason}", own, exception.Message);
                return default;
            }
        }

        if (material.Id is not string id)
        {
            // A layer that names no stage and no material is drawn with the program of the engine.
            return default;
        }

        if (_programs.TryGetValue(id, out MaterialProgram compiled))
        {
            return compiled.Shader;
        }

        if (_failed.ContainsKey(id))
        {
            return default;
        }

        if (!_declared.TryGetValue(id, out Declared declared))
        {
            _failed[id] = new KeyNotFoundException($"The content holds no material under the identifier '{id}'.");
            _missing.Add(id);
            _logger?.LogError("The layer names the material '{Material}', and no document of the content declares one, so the layer is drawn with the program of the engine.", id);
            return default;
        }

        try
        {
            ShaderHandle shader = _shaders.Load(declared.Fragment, declared.Vertex);
            _programs[id] = new MaterialProgram(shader, declared.Uniforms);
            return shader;
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or InvalidOperationException or ObjectDisposedException or FormatException or ArgumentException)
        {
            _failed[id] = exception;
            _missing.Add(id);
            _logger?.LogError("The material '{Material}' cannot be read, so the layer is drawn with the program of the engine: {Reason}", id, exception.Message);
            return default;
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    void IMaterialService.SetShader(IRenderer renderer, ShaderHandle shader, Material material)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        // A layer that draws with the program of the engine reads no uniform of a material: the values of a document belong to a
        // stage, and a program that was not compiled from one would be handed names it does not declare.
        if (shader == default || material.Id is not string id || !_declared.TryGetValue(id, out Declared declared))
        {
            return;
        }

        foreach (IMaterialService.UniformValue uniform in declared.Uniforms)
        {
            Send(renderer, uniform);
        }
    }

    /// <inheritdoc />
    void IMaterialService.SetShader(IRenderer renderer, ShaderHandle shader, ReadOnlySpan<IMaterialService.UniformValue> uniforms)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        if (shader == default)
        {
            return;
        }

        foreach (IMaterialService.UniformValue uniform in uniforms)
        {
            Send(renderer, uniform);
        }
    }

    /// <summary>Sends one value that a document declared to a renderer, which draws the quads that follow with it.</summary>
    /// <remarks>
    /// A value reaches the renderer by the kind it was read as, and the kind is not a guess: a document that says <c>int</c> is
    /// handed to the overload that sets a whole number, so a stage that declares an <c>int</c> is given an <c>int</c> rather than a
    /// float that the device would have converted. Everything else is one number or a vector of them.
    /// </remarks>
    private static void Send(IRenderer renderer, in IMaterialService.UniformValue uniform)
    {
        if (uniform.Kind == IMaterialService.UniformKind.Int)
        {
            renderer.SetUniform(uniform.Name, (int)uniform.Numbers.Span[0]);
            return;
        }

        if (uniform.Numbers.Length == 1)
        {
            renderer.SetUniform(uniform.Name, uniform.Numbers.Span[0]);
            return;
        }

        renderer.SetUniform(uniform.Name, uniform.Numbers.Span);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _programs.Clear();
        _declared.Clear();
        _failed.Clear();
    }

    /// <summary>A material of the content after its fields were read: the stages of its shader and the values of its uniforms.</summary>
    /// <param name="Fragment">The path of the fragment stage, relative to the game root.</param>
    /// <param name="Vertex">The path of the vertex stage, or null for the stage of the engine.</param>
    /// <param name="Uniforms">The values of the uniforms, read into the kinds and the numbers a renderer is handed.</param>
    private readonly record struct Declared(string Fragment, string? Vertex, IMaterialService.UniformValue[] Uniforms);

    /// <summary>The program that a material was compiled into, with the values of the uniforms that the program sends.</summary>
    /// <param name="Shader">The handle of the program.</param>
    /// <param name="Uniforms">The values of the uniforms of the material, in the order the document wrote them.</param>
    private readonly record struct MaterialProgram(ShaderHandle Shader, IMaterialService.UniformValue[] Uniforms);
}
