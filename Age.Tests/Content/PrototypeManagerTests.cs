using System.Text.Json;
using Age.Content.Prototypes;
using Age.Core;
using Age.Physics;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Age.Tests;

public sealed class PrototypeManagerTests
{
    [Fact]
    public void PrototypeManager_Build_MergesWhatAPrototypeInheritsWithWhatItDeclares()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("base.yml", """
            - type: thing
              id: Base
              components:
                - type: Transform
                  Position:
                    X: 1
                    Y: 2
            """);
        prototypes.Add("sword.yml", """
            - type: thing
              id: Sword
              parent: Base
              components:
                - type: Transform
                  Position:
                    X: 9
                    Y: 9
                - type: Collider
                  Size:
                    X: 8
                    Y: 8
            """);

        prototypes.Build().Should().Be(2);
        prototypes.Ids.Should().Equal("Base", "Sword");

        Prototype sword = prototypes.Get<Prototype>("Sword");
        sword.Kind.Should().Be("thing");
        sword.Parent.Should().Be("Base");
        sword.Components.Select(component => component.Name).Should().Equal("Transform", "Collider");
        sword.Components.Should().HaveCount(2, "the parent gives what the child does not declare");
        sword.TryGet("Transform", out PrototypeComponent? transform).Should().BeTrue();

        JsonElement position = transform!.Values.GetProperty("Position");
        position.GetProperty("X").GetInt32().Should().Be(9, "what a prototype declares wins over what it inherits");
        transform.File.Should().Be("sword.yml");
    }

    [Fact]
    public void PrototypeManager_Add_RefusesTheSameIdentifierTwiceAndNamesBothFiles()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("base.yml", "- id: Sword\n  type: thing");

        Action again = () => prototypes.Add("other.yml", "- id: Sword\n  type: thing");

        PrototypeException error = again.Should().Throw<PrototypeException>().Subject.Single();
        error.File.Should().Be("other.yml");
        error.Line.Should().Be(1);
        error.Message.Should().Contain("base.yml", "the message says where the identifier was already declared");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesAFieldThatIsNotAPrototypeField()
    {
        PrototypeManager prototypes = Create();

        Action add = () => prototypes.Add("sword.yml", "- id: Sword\n  type: thing\n  damage: 5");

        PrototypeException error = add.Should().Throw<PrototypeException>().Subject.Single();
        error.Line.Should().Be(3);
        error.Message.Should().Contain("'damage'").And.Contain("components");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesAComponentThatNothingKnows()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("sword.yml", """
            - id: Sword
              type: thing
              components:
                - type: Mystery
            """);

        Action build = () => prototypes.Build();

        PrototypeException error = build.Should().Throw<PrototypeException>().Subject.Single();
        error.File.Should().Be("sword.yml");
        error.Line.Should().Be(4);
        error.Message.Should().Contain("Mystery").And.Contain("not registered");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesValuesThatTheContractCannotRead()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("sword.yml", """
            - id: Sword
              type: thing
              components:
                - type: Transform
                  Position: 5
            """);

        Action build = () => prototypes.Build();

        build.Should().Throw<PrototypeException>().WithMessage("*Transform*").And.Message.Should().Contain("cannot be read");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesAFieldThatTheComponentDoesNotHave()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("sword.yml", """
            - id: Sword
              type: thing
              components:
                - type: Transform
                  Mystery: 1
            """);

        Action build = () => prototypes.Build();

        build.Should().Throw<PrototypeException>().WithMessage("*Transform*");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesAParentThatNoDocumentDeclares()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("sword.yml", "- id: Sword\n  type: thing\n  parent: Missing");

        Action build = () => prototypes.Build();

        PrototypeException error = build.Should().Throw<PrototypeException>().Subject.Single();
        error.File.Should().Be("sword.yml");
        error.Line.Should().Be(1);
        error.Message.Should().Contain("Missing");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesPrototypesThatInheritFromEachOther()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("circle.yml", "- id: A\n  type: thing\n  parent: B\n- id: B\n  type: thing\n  parent: A");

        Action build = () => prototypes.Build();

        build.Should().Throw<PrototypeException>().WithMessage("*inherits from itself*");
    }

    [Fact]
    public void PrototypeManager_Build_RefusesAKindThatNothingRegistered()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("sword.yml", "- id: Sword\n  type: weapon");

        Action build = () => prototypes.Build();

        build.Should().Throw<PrototypeException>().WithMessage("*kind 'weapon'*");
    }

    [Fact]
    public void PrototypeManager_APrototypeThatIsNotThere_IsReported()
    {
        PrototypeManager prototypes = Create();
        prototypes.Add("sword.yml", "- id: Sword\n  type: thing");
        prototypes.Build();

        prototypes.Count.Should().Be(1);
        prototypes.Has<Prototype>("Sword").Should().BeTrue();
        prototypes.TryGet<Prototype>("Axe", out Prototype? axe).Should().BeFalse();
        axe.Should().BeNull();
        prototypes.Enumerate<Prototype>().Should().ContainSingle().Which.Id.Should().Be("Sword");

        Action missing = () => prototypes.Get<Prototype>("Axe");

        missing.Should().Throw<KeyNotFoundException>().WithMessage("*Axe*");
    }

    [Fact]
    public void PrototypeManager_Add_ADocumentThatIsNotAList_IsRefused()
    {
        PrototypeManager prototypes = Create();

        Action add = () => prototypes.Add("sword.yml", "id: Sword");

        add.Should().Throw<PrototypeException>().WithMessage("*list of prototypes*");
    }

    [Fact]
    public void ProtoId_WritesAndReadsTheWordItHolds()
    {
        var reference = new ProtoId<Prototype>("Sword");

        string json = JsonSerializer.Serialize(reference);

        json.Should().Be("\"Sword\"", "a reference is a word, which is what keeps a scene readable");
        JsonSerializer.Deserialize<ProtoId<Prototype>>(json).Should().Be(reference);
        JsonSerializer.Deserialize<ProtoId<Prototype>>("null").Should().Be(ProtoId<Prototype>.None, "a scene that holds no prototype says so with null");

        ProtoId<Prototype>.None.HasValue.Should().BeFalse();
        ProtoId<Prototype>.None.ToString().Should().BeEmpty();
        new ProtoId<Prototype>("Sword").Should().NotBe(new ProtoId<Prototype>("Axe"));
        default(ProtoId<Prototype>).Should().Be(ProtoId<Prototype>.None);
    }

    /// <summary>Builds a manager over the components of the engine, with one kind of prototype that holds the data itself.</summary>
    private static PrototypeManager Create()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddAgeCore()
            .AddAgePhysics()
            .AddAgeUI()
            .AddAgeRendering()
            .BuildServiceProvider();

        var prototypes = new PrototypeManager(provider.GetRequiredService<ComponentRegistry>());
        prototypes.Register<Prototype>("thing", prototype => prototype);
        return prototypes;
    }
}
