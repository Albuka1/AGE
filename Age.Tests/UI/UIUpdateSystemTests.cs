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

    [Fact]
    public void UIUpdateSystem_AClickOnADropdown_OpensItAndChoosingARowChangesTheValue()
    {
        var world = new World();
        Entity dropdown = world.CreateEntity();
        world.Set(dropdown, new RectTransformComponent { Position = new Vector2(0f, 0f), Size = new Vector2(120f, 24f), Visible = true });
        world.Set(dropdown, new DropdownComponent { Options = ["low", "medium", "high"], Selected = 0, RowHeight = 24f, Interactable = true });
        var input = new NullInputService();
        var system = new UIUpdateSystem(input);
        var changes = new List<DropdownChangedEvent>();
        world.Events.Subscribe<DropdownChangedEvent>((_, e) => changes.Add(e));

        // A press on the closed element opens the list, which hangs below it.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(60f, 12f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<DropdownComponent>(dropdown).Open.Should().BeTrue();

        // The list stands from y = 24 to y = 96, so a press at y = 60 is the second row, which chooses the second value.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(60f, 60f), false);
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(60f, 60f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));
        world.Events.Dispatch();

        world.Get<DropdownComponent>(dropdown).Open.Should().BeFalse("choosing a row closes the list");
        world.Get<DropdownComponent>(dropdown).Selected.Should().Be(1);
        world.Get<DropdownComponent>(dropdown).Value.Should().Be("medium");
        changes.Should().ContainSingle().Which.Value.Should().Be("medium");
    }

    [Fact]
    public void UIUpdateSystem_AClickOnAnOpenList_DoesNotAlsoPressTheButtonUnderIt()
    {
        var world = new World();
        var input = new NullInputService();
        var system = new UIUpdateSystem(input);

        // A button that the open list of a drop-down hangs over: the two overlap where the second row of the list is.
        Entity button = world.CreateEntity();
        world.Set(button, new RectTransformComponent { Position = new Vector2(0f, 40f), Size = new Vector2(120f, 24f), Visible = true });
        world.Set(button, new ButtonComponent { Interactable = true });

        Entity dropdown = world.CreateEntity();
        world.Set(dropdown, new RectTransformComponent { Position = new Vector2(0f, 0f), Size = new Vector2(120f, 24f), Visible = true });
        world.Set(dropdown, new DropdownComponent { Options = ["low", "medium", "high"], Selected = 0, RowHeight = 24f, Open = true, Interactable = true });

        var presses = new List<ButtonPressedEvent>();
        world.Events.Subscribe<ButtonPressedEvent>((_, e) => presses.Add(e));

        // The press lands on the second row of the open list, which is drawn over the button: the list takes it, so the button is
        // neither pressed nor hovered, which is what an open list that the pointer can choose from means.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(60f, 60f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));
        world.Events.Dispatch();

        world.Get<DropdownComponent>(dropdown).Selected.Should().Be(1, "the click chose the row it landed on");
        world.Get<ButtonComponent>(button).IsPressed.Should().BeFalse("the list that covered the button took the click");
        world.Get<ButtonComponent>(button).IsHovered.Should().BeFalse();
        presses.Should().BeEmpty();
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
