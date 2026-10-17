using Age.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins what the scan reads off a class to what the container holds: a system is registered under itself and under every
/// interface of it that carries the attribute, and a class that says nothing is left alone.
/// </summary>
public sealed class SystemRegistrationExtensionsTests
{
    [Fact]
    public void AddAgeSystems_AClassWithTheAttribute_IsRegisteredUnderItself()
    {
        using ServiceProvider provider = Provider();
        provider.GetService<MarkedSystem>().Should().NotBeNull();
    }

    [Fact]
    public void AddAgeSystems_AClassThatAnswersAMarkedInterface_IsReachedByBothNamesAndIsOneObject()
    {
        using ServiceProvider provider = Provider();

        ITestSystem byInterface = provider.GetRequiredService<ITestSystem>();
        AnsweringSystem byType = provider.GetRequiredService<AnsweringSystem>();

        byInterface.Should().BeSameAs(byType, "the interface is a name of the same object, not a second one");
    }

    [Fact]
    public void AddAgeSystems_AClassWithTwoMarkedInterfaces_IsOneObjectUnderEveryName()
    {
        using ServiceProvider provider = Provider();

        IFirst first = provider.GetRequiredService<IFirst>();
        ISecond second = provider.GetRequiredService<ISecond>();
        IFirst again = provider.GetRequiredService<IFirst>();

        first.Should().BeSameAs(second);
        first.Should().BeSameAs(again);
    }

    [Fact]
    public void AddAgeSystems_AClassThatSaysNothing_IsLeftAlone()
    {
        using ServiceProvider provider = Provider();
        provider.GetService<UnmarkedClass>().Should().BeNull("a class that carries no attribute and answers no marked interface is not a system");
    }

    [Fact]
    public void AddAgeSystems_AnAbstractClassOrAnInterface_IsLeftAlone()
    {
        using ServiceProvider provider = Provider();

        provider.GetService<AbstractMarked>().Should().BeNull();
        provider.GetService<ITestSystem>().Should().NotBeNull("the interface is a name a concrete class was registered under");
    }

    [Fact]
    public void AddAgeComponentRegistrations_AClassWithTheAttribute_IsRegisteredUnderTheInterface()
    {
        using ServiceProvider provider = Provider();

        provider.GetServices<IComponentRegistrations>()
            .Should().Contain(registration => registration is MarkedRegistrations);
    }

    [Fact]
    public void AddAgeComponentRegistrations_AClassWithoutTheAttribute_IsLeftAlone()
    {
        using ServiceProvider provider = Provider();

        provider.GetServices<IComponentRegistrations>()
            .Should().NotContain(registration => registration is UnmarkedRegistrations, "the interface alone is what a test implements to pin a component down");
    }

    [Fact]
    public void AddAgeSystems_AClassThatCarriesTheAttributeTwice_IsRegisteredOnce()
    {
        using ServiceProvider provider = Provider();
        IEnumerable<MarkedSystem> systems = provider.GetServices<MarkedSystem>();

        systems.Should().ContainSingle("a class is a name of one object, whatever its attributes say");
    }

    private static ServiceProvider Provider() =>
        new ServiceCollection()
            .AddAgeSystems(typeof(SystemRegistrationExtensionsTests).Assembly)
            .AddAgeComponentRegistrations(typeof(SystemRegistrationExtensionsTests).Assembly)
            .BuildServiceProvider();

    [System]
    private sealed class MarkedSystem;

    [SystemInterface]
    private interface ITestSystem;

    private sealed class AnsweringSystem : ITestSystem;

    [SystemInterface]
    private interface IFirst;

    [SystemInterface]
    private interface ISecond;

    private sealed class TwoInterfaces : IFirst, ISecond;

    private sealed class UnmarkedClass;

    [System]
    private abstract class AbstractMarked;

    [ComponentRegistration]
    private sealed class MarkedRegistrations : IComponentRegistrations
    {
        public void Register(ComponentRegistry registry)
        {
        }
    }

    private sealed class UnmarkedRegistrations : IComponentRegistrations
    {
        public void Register(ComponentRegistry registry)
        {
        }
    }
}
