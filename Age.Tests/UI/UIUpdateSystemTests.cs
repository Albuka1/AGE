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

    [Fact]
    public void UIUpdateSystem_APressOnACheckBox_TogglesItAndRaisesTheEvent()
    {
        var world = new World();
        Entity box = world.CreateEntity();
        world.Set(box, new RectTransformComponent { Position = Vector2.Zero, Size = new Vector2(24f, 24f), Visible = true });
        world.Set(box, new CheckBoxComponent { Checked = false, Interactable = true });
        var input = new NullInputService();
        var system = new UIUpdateSystem(input);
        var changes = new List<CheckChangedEvent>();
        world.Events.Subscribe<CheckChangedEvent>((_, e) => changes.Add(e));

        // The input service reports a press for the frame it was opened in, so the state is written and then read: BeginFrame twice is
        // what the loop of a game does while a button stays held, and what the button test beside this one does.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));
        world.Events.Dispatch();

        world.Get<CheckBoxComponent>(box).Checked.Should().BeTrue("a press on the box toggles it");
        world.Get<CheckBoxComponent>(box).IsHovered.Should().BeTrue();
        changes.Should().ContainSingle().Which.Checked.Should().BeTrue();
    }

    [Fact]
    public void UIUpdateSystem_ADragOnASlider_FollowsThePointerAndTheTrack()
    {
        var world = new World();
        Entity slider = world.CreateEntity();
        world.Set(slider, new RectTransformComponent { Position = new Vector2(100f, 0f), Size = new Vector2(200f, 20f), Visible = true });
        world.Set(slider, new SliderComponent { Minimum = 0f, Maximum = 10f, Interactable = true });
        var input = new NullInputService();
        var system = new UIUpdateSystem(input);

        // A press at the middle of a track that runs from 100 to 300 is halfway along it, so the number is halfway through the range.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(200f, 10f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<SliderComponent>(slider).IsDragging.Should().BeTrue();
        world.Get<SliderComponent>(slider).Value.Should().BeApproximately(5f, 0.001f);

        // The drag follows the pointer past the end of the track, and the number stays at the top of the range rather than running past it.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(500f, 10f), true);
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<SliderComponent>(slider).Value.Should().BeApproximately(10f, 0.001f, "the number is kept inside the range the slider declares");

        // Letting go ends the drag, and the pointer that moves on no longer changes the number.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(500f, 10f), false);
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<SliderComponent>(slider).IsDragging.Should().BeFalse();

        input.BeginFrame();
        input.State = new UIInputState(new Vector2(100f, 10f), false);
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<SliderComponent>(slider).Value.Should().BeApproximately(10f, 0.001f, "a slider that is not dragged does not follow the pointer");
    }
}
