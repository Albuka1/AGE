using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Age.SourceGen;

/// <summary>
/// Writes the component registrations and the serialization contracts of an assembly from the <c>[Component]</c>
/// attributes of its types.
/// </summary>
/// <remarks>
/// A component is declared once: the attribute names it, and this generator turns that name into the registration that the
/// scene serializer reads and into the <c>[JsonSerializable]</c> entry of the context that gives the registry its
/// contract. Without it, one component costs four edits in two files, and a forgotten one is a component that turns out to
/// be unsavable after a restart.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ComponentRegistrationGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Age.Core.ComponentAttribute";

    /// <summary>The name of the property that names the generated registrations class, and its default.</summary>
    private const string RegistrationsProperty = "build_property.AgeComponentRegistrations";

    /// <summary>The name of the property that names the generated context, and its default.</summary>
    private const string ContextProperty = "build_property.AgeComponentsJsonContext";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ComponentModel> components = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (attribute, cancellationToken) => Describe(attribute, cancellationToken));

        IncrementalValueProvider<GeneratorOptions> options = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out string? rootNamespace);
            provider.GlobalOptions.TryGetValue(RegistrationsProperty, out string? registrations);
            provider.GlobalOptions.TryGetValue(ContextProperty, out string? jsonContext);

            // A property that is declared but left empty counts as missing, which is what a project that says nothing
            // about the names of the generated code gets.
            return new GeneratorOptions(
                Or(rootNamespace, "Age"),
                Or(registrations, "GeneratedComponentRegistrations"),
                Or(jsonContext, "GeneratedComponentsJsonContext"));
        });

        context.RegisterSourceOutput(
            components.Collect().Combine(options),
            static (production, source) => Emit(production, source.Left, source.Right));
    }

    /// <summary>Returns the value that a project supplied, or the fallback when the project supplied none.</summary>
    /// <remarks>The `!` is what netstandard2.0 needs: its reference assemblies do not say that the check leaves a value behind.</remarks>
    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value!;

    /// <summary>Reads what is known about one component at compile time.</summary>
    private static ComponentModel Describe(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        string name = string.Empty;
        var scene = true;

        foreach (AttributeData attribute in context.Attributes)
        {
            if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string declared)
            {
                name = declared;
            }

            foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
            {
                if (argument.Key == "Scene" && argument.Value.Value is bool holds)
                {
                    scene = holds;
                }
            }
        }

        return new ComponentModel(
            name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.Name,
            scene,
            type.Locations.Length > 0 ? type.Locations[0] : Location.None,
            type.TypeKind,
            type.AllInterfaces.Any(implemented => implemented.ToDisplayString() == "Age.Core.IComponent"));
    }

    /// <summary>Writes the registrations and the context of an assembly, and reports a component that cannot be one.</summary>
    private static void Emit(SourceProductionContext context, ImmutableArray<ComponentModel> components, GeneratorOptions options)
    {
        var declared = new List<ComponentModel>();

        foreach (ComponentModel component in components)
        {
            if (component.Kind != TypeKind.Struct)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotAStruct, component.Location, component.Symbol, component.Name));
                continue;
            }

            if (!component.ImplementsComponent)
            {
                context.ReportDiagnostic(Diagnostic.Create(NotAComponent, component.Location, component.Symbol, component.Name));
                continue;
            }

            declared.Add(component);
        }

        if (declared.Count == 0)
        {
            return;
        }

        // A name decides which component a line of a scene reaches, so the order of the registrations follows the names
        // rather than the order the compiler happened to visit the types in.
        declared.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        var registrations = new StringBuilder();
        registrations.AppendLine("// <auto-generated />");
        registrations.AppendLine("#pragma warning disable CS1591");
        registrations.AppendLine();
        registrations.Append("namespace ").AppendLine(options.Namespace);
        registrations.AppendLine("{");
        registrations.Append("    internal sealed class ").Append(options.RegistrationsName).AppendLine(" : global::Age.Core.IComponentRegistrations");
        registrations.AppendLine("    {");
        registrations.AppendLine("        public void Register(global::Age.Core.ComponentRegistry registry)");
        registrations.AppendLine("        {");
        registrations.AppendLine("            global::System.ArgumentNullException.ThrowIfNull(registry);");

        foreach (ComponentModel component in declared.Where(component => component.Scene))
        {
            registrations
                .Append("            registry.Register(").Append(SymbolDisplay.FormatLiteral(component.Name, quote: true)).Append(", ")
                .Append(options.Namespace).Append('.').Append(options.JsonContextName).Append(".Default.")
                .Append(component.Symbol).AppendLine(");");
        }

        registrations.AppendLine("        }");
        registrations.AppendLine("    }");
        registrations.AppendLine("}");

        // A context of serialization is not written here, and cannot be: the source generator of System.Text.Json is a
        // source generator itself, so it never sees what another one adds to the compilation. The [JsonSerializable] entry
        // of a component therefore stays in the context of its assembly, where a missing one is a compile error of the
        // generated registration rather than a component that turns out to be unsavable after a restart.
        context.AddSource($"{options.RegistrationsName}.Components.g.cs", SourceText.From(registrations.ToString(), Encoding.UTF8));
    }

    /// <summary>The error that a type marked as a component is not a value type.</summary>
    private static readonly DiagnosticDescriptor NotAStruct = new(
        "AGE0001",
        "A component has to be a struct",
        "The type '{0}' is declared with [Component(\"{1}\")], and a component is a value type: make it a struct",
        "Age",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The error that a type marked as a component does not implement <c>IComponent</c>.</summary>
    private static readonly DiagnosticDescriptor NotAComponent = new(
        "AGE0002",
        "A component has to implement IComponent",
        "The type '{0}' is declared with [Component(\"{1}\")] but does not implement Age.Core.IComponent",
        "Age",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>What is known about one component at compile time.</summary>
    private readonly struct ComponentModel
    {
        /// <summary>Initializes what is known about a component.</summary>
        public ComponentModel(string name, string type, string symbol, bool scene, Location location, TypeKind kind, bool implementsComponent)
        {
            Name = name;
            Type = type;
            Symbol = symbol;
            Scene = scene;
            Location = location;
            Kind = kind;
            ImplementsComponent = implementsComponent;
        }

        /// <summary>Gets the name that a scene file uses.</summary>
        public string Name { get; }

        /// <summary>Gets the type of the component, qualified the way the generated code needs it.</summary>
        public string Type { get; }

        /// <summary>Gets the name of the type, which is the property of the context that holds its contract.</summary>
        public string Symbol { get; }

        /// <summary>Gets a value indicating whether a scene holds the component.</summary>
        public bool Scene { get; }

        /// <summary>Gets the location of the declaration, which a diagnostic points at.</summary>
        public Location Location { get; }

        /// <summary>Gets the kind of the type, which has to be a struct.</summary>
        public TypeKind Kind { get; }

        /// <summary>Gets a value indicating whether the type implements <c>IComponent</c>.</summary>
        public bool ImplementsComponent { get; }
    }

    /// <summary>The names that the generated code uses, which the project of an assembly sets.</summary>
    private readonly struct GeneratorOptions
    {
        /// <summary>Initializes the names of the generated code.</summary>
        public GeneratorOptions(string @namespace, string registrationsName, string jsonContextName)
        {
            Namespace = @namespace;
            RegistrationsName = registrationsName;
            JsonContextName = jsonContextName;
        }

        /// <summary>Gets the namespace of the assembly, which the generated code is written into.</summary>
        public string Namespace { get; }

        /// <summary>Gets the name of the generated registrations class.</summary>
        public string RegistrationsName { get; }

        /// <summary>Gets the name of the generated serialization context.</summary>
        public string JsonContextName { get; }
    }
}
