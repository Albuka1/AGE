using Age.Core;
using Age.Input;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins the text field of the interface to what a person does with it: a press gives it focus, the characters of the frame land in the
/// line at the caret, and a press outside takes the focus away so nothing is typed where it was not meant to be.
/// </summary>
public sealed class TextInputSystemTests
{
    [Fact]
    public void TextInputSystem_APressOnAField_FocusesItAndTypingWritesIntoIt()
    {
        var world = new World();
        Entity field = CreateField(world, "hero");
        var input = new NullInputService();
        var system = new TextInputSystem(input, input);

        // A press inside the rectangle of the field gives it focus.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<TextFieldComponent>(field).Focused.Should().BeTrue("a press inside the field is what focuses it");

        // The caret is at the end of "hero", so a typed "!" lands there.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), false);
        input.BeginFrame();
        input.Typed = "!";
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<TextFieldComponent>(field).Value.Should().Be("hero!");
    }

    [Fact]
    public void TextInputSystem_APressOutsideEveryField_TakesTheFocusAway()
    {
        var world = new World();
        Entity field = CreateField(world, "name");
        var input = new NullInputService();
        var system = new TextInputSystem(input, input);

        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));
        world.Get<TextFieldComponent>(field).Focused.Should().BeTrue();

        // The button comes up first, so the press outside is a fresh press rather than a button that is still held.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), false);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        // A press far from the field ends the focus, and the characters of the next frame land nowhere.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(900f, 700f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<TextFieldComponent>(field).Focused.Should().BeFalse("a press outside the field takes the focus away");

        input.BeginFrame();
        input.State = new UIInputState(new Vector2(900f, 700f), false);
        input.BeginFrame();
        input.Typed = "x";
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<TextFieldComponent>(field).Value.Should().Be("name", "a field with no focus reads nothing");
    }

    [Fact]
    public void TextInputSystem_ASecondField_ClickedLaterTakesTheFocusFromTheFirst()
    {
        var world = new World();
        Entity first = CreateField(world, "one", new Vector2(0f, 0f));
        Entity second = CreateField(world, "two", new Vector2(0f, 100f));
        var input = new NullInputService();
        var system = new TextInputSystem(input, input);

        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        // The button comes up before the second press, or the input service reports the button as held rather than pressed again.
        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 10f), false);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        input.BeginFrame();
        input.State = new UIInputState(new Vector2(10f, 110f), true);
        input.BeginFrame();
        system.UpdateFrame(world, new GameTime(0d, 0d));

        world.Get<TextFieldComponent>(first).Focused.Should().BeFalse("only one field is typed into at a time");
        world.Get<TextFieldComponent>(second).Focused.Should().BeTrue();
    }

    private static Entity CreateField(World world, string value, Vector2? position = null)
    {
        Entity field = world.CreateEntity();
        world.Set(field, new RectTransformComponent { Position = position ?? new Vector2(0f, 0f), Size = new Vector2(200f, 28f), Visible = true });
        world.Set(field, new TextFieldComponent { Text = value, Interactable = true });
        return field;
    }
}
