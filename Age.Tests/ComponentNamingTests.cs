using System.Reflection;
using Age.Core;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class ComponentNamingTests
{
    [Fact]
    public void EveryComponentOfTheEngine_IsDeclaredWithTheNameOfItsSceneEntry()
    {
        List<Type> components = [.. Components()];

        components.Should().NotBeEmpty("the engine has components and every one of them has to be reachable from a scene");

        foreach (Type component in components)
        {
            ComponentAttribute? attribute = component.GetCustomAttribute<ComponentAttribute>();

            attribute.Should().NotBeNull($"{component.Name} has to say what a scene calls it, with [Component(...)]");
            attribute!.Name.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void EveryComponentOfTheEngine_IsRegisteredUnderThatName()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddAgeCore()
            .AddAgePhysics()
            .AddAgeUI()
            .AddAgeRendering()
            .BuildServiceProvider();

        ComponentRegistry registry = provider.GetRequiredService<ComponentRegistry>();

        foreach (Type component in Components())
        {
            string name = component.GetCustomAttribute<ComponentAttribute>()!.Name;

            registry.TryGetType(name, out Type? registered).Should().BeTrue($"'{name}' has to be registered, so that a scene can hold {component.Name}");
            registered.Should().Be(component);
        }
    }

    [Fact]
    public void TheNamesOfTheComponents_AreUnique()
    {
        IEnumerable<string> names = Components().Select(component => component.GetCustomAttribute<ComponentAttribute>()!.Name);

        names.Should().OnlyHaveUniqueItems("a scene that names a component has to resolve to exactly one type");
    }

    /// <summary>Returns every public component type of the engine assemblies, which are the ones a scene can hold.</summary>
    private static IEnumerable<Type> Components()
    {
        Assembly[] assemblies =
        [
            typeof(World).Assembly,
            typeof(ColliderComponent).Assembly,
            typeof(ButtonComponent).Assembly,
            typeof(SpriteComponent).Assembly,
        ];

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsPublic: true, IsValueType: true } && typeof(IComponent).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);
    }
}
