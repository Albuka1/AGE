using Age.Core;
using Age.Input;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class UIUpdateSystemTests
{
    [Fact]
    public void UIUpdateSystem_TopmostElementIsPressed()
    {
        var world = new World();
        Entity lower = CreateButton(world, zOrder: 0);
        Entity upper = CreateButton(world, zOrder: 1);
        var input = new NullInputService { State = new UIInputState(new Vector2(20f, 20f), true) };
        var system = new UIUpdateSystem(input);

        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<ButtonComponent>(upper).IsPressed.Should().BeTrue();
        world.Get<ButtonComponent>(upper).IsHovered.Should().BeTrue();
        world.Get<ButtonComponent>(lower).IsPressed.Should().BeFalse();
        world.Get<ButtonComponent>(lower).IsHovered.Should().BeFalse();
    }

    [Fact]
    public void UIUpdateSystem_PressOnAButton_RaisesThePressedEventOnce()
    {
        var world = new World();
        Entity button = CreateButton(world, zOrder: 0);
        var input = new NullInputService();
        var system = new UIUpdateSystem(input);
        var pressed = new List<Entity>();
        world.Events.Subscribe<ButtonPressedEvent>((_, e) => pressed.Add(e.Button));

        input.BeginFrame();
        input.State = new UIInputState(new Vector2(20f, 20f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));
        world.Events.Dispatch();

        pressed.Should().ContainSingle().Which.Should().Be(button);

        input.BeginFrame();   // the frame the loop opens while the button is still held
        system.UpdateFrame(world, new GameTime(0d, 0d));
        world.Events.Dispatch();

        pressed.Should().ContainSingle("holding the button does not press it again");
    }

    private static Entity CreateButton(World world, int zOrder)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(100f, 100f), ZOrder = zOrder, Visible = true });
        world.Set(entity, new ButtonComponent { BaseColor = Color.White, Interactable = true });
        return entity;
    }
}
