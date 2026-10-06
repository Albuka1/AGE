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

        system.Update(world, new GameTime(0d, 0d));

        world.Get<ButtonComponent>(upper).IsPressed.Should().BeTrue();
        world.Get<ButtonComponent>(upper).IsHovered.Should().BeTrue();
        world.Get<ButtonComponent>(lower).IsPressed.Should().BeFalse();
        world.Get<ButtonComponent>(lower).IsHovered.Should().BeFalse();
    }

    private static Entity CreateButton(World world, int zOrder)
    {
        Entity entity = world.CreateEntity();
        world.Set(entity, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(100f, 100f), ZOrder = zOrder, Visible = true });
        world.Set(entity, new ButtonComponent { BaseColor = Color.White, Interactable = true });
        return entity;
    }
}
