using Age.Core;
using Age.Rendering;
using Age.UI;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

/// <summary>
/// Pins what a builder of a window does to a world: one entity per element, the components of the interface on each of them,
/// and the tree that the layout of the interface walks.
/// </summary>
public sealed class UiBuilderTests
{
    [Fact]
    public void UiBuilder_Root_IsAPanelThatDrawsNothingOfItsOwn()
    {
        var world = new World();
        Entity canvas = CreateCanvas(world);
        var ui = new UiBuilder(world, canvas);
        Entity root = ui.Root.Entity;

        world.IsAlive(root).Should().BeTrue();
        world.Has<PanelComponent>(root).Should().BeTrue("the root of a window is a panel that its elements stand on");
        world.Get<PanelComponent>(root).Color.A.Should().Be(0, "the root draws nothing of its own until a window colours it");

        world.Resolve(world.Get<ParentComponent>(root).Parent).Should().Be(canvas);
        (world.Get<ChildrenComponent>(canvas).Children ?? []).Should().Contain(world.Reference(root));
    }

    [Fact]
    public void UiBuilder_Label_IsATextWithAStyleAndAName()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity label = ui.Label("title").Text("Inventory").Entity;

        world.Has<TextComponent>(label).Should().BeTrue("a label of an interface is a text of the engine");
        world.Get<TextComponent>(label).Text.Should().Be("Inventory");
        world.Get<TextComponent>(label).Style.VerticalAlign.Should().Be(TextVerticalAlign.Middle, "a label of a window stands in the middle of the row it is drawn in");
        world.Get<UiElementComponent>(label).Id.Should().Be("title");
        ui.Elements["title"].Should().Be(label);
    }

    [Fact]
    public void UiBuilder_APanelOfABlock_HoldsTheElementsOfIt()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity panel;
        Entity inside;

        using (ui.Panel("content", p => p.At(0f, 0f).Size(400f, 300f)))
        {
            inside = ui.Label("title").Entity;
            panel = world.Resolve(world.Get<ParentComponent>(inside).Parent);
        }

        // The name of a panel is what the elements between its braces stand inside.
        world.Get<UiElementComponent>(panel).Id.Should().Be("content");
        world.Resolve(world.Get<ParentComponent>(inside).Parent).Should().Be(panel);
        (world.Get<ChildrenComponent>(panel).Children ?? []).Should().Contain(world.Reference(inside));
    }

    [Fact]
    public void UiBuilder_AnElementAfterABlock_StandsInTheParentOfThePanel()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity inside;
        Entity after;

        using (ui.Panel("content", _ => { }))
        {
            inside = ui.Label("title").Entity;
        }

        after = ui.Label("footer").Entity;

        // The block ends where its braces end, so an element that follows stands in the parent of the panel rather than inside it.
        world.Resolve(world.Get<ParentComponent>(inside).Parent).Should().NotBe(world.Resolve(world.Get<ParentComponent>(after).Parent));
        world.Resolve(world.Get<ParentComponent>(after).Parent).Should().Be(ui.Root.Entity);
    }

    [Fact]
    public void UiBuilder_TwoElementsOfOneName_AreRefused()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));

        ui.Label("title").Text("one");

        Action again = () => ui.Label("title");

        again.Should().Throw<InvalidOperationException>().WithMessage("*title*", "a name is what a window reads its elements back by");
    }

    [Fact]
    public void UiBuilder_AtAndSize_WriteTheRectangleOfTheElement()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity panel = ui.Panel("content").At(20f, 30f).Size(200f, 100f).Entity;
        RectTransformComponent rect = world.Get<RectTransformComponent>(panel);

        rect.Anchored.Should().BeTrue();
        rect.AnchorMin.Should().Be(Vector2.Zero);
        rect.AnchorMax.Should().Be(Vector2.Zero);
        rect.Pivot.Should().Be(Vector2.Zero);
        rect.AnchoredPosition.Should().Be(new Vector2(20f, 30f));
        rect.SizeDelta.Should().Be(new Vector2(200f, 100f));
    }

    [Fact]
    public void UiBuilder_Center_PutsTheElementInTheMiddleOfItsParent()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity panel = ui.Panel("content").Center().Size(200f, 100f).Entity;
        RectTransformComponent rect = world.Get<RectTransformComponent>(panel);

        rect.AnchorMin.Should().Be(new Vector2(0.5f, 0.5f));
        rect.AnchorMax.Should().Be(new Vector2(0.5f, 0.5f));
        rect.Pivot.Should().Be(new Vector2(0.5f, 0.5f));
        rect.AnchoredPosition.Should().Be(Vector2.Zero);
    }

    [Fact]
    public void UiBuilder_Stretch_MakesTheElementTakeTheRoomOfItsParent()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity panel = ui.Panel("content").Stretch().Entity;
        RectTransformComponent rect = world.Get<RectTransformComponent>(panel);

        rect.AnchorMin.Should().Be(Vector2.Zero);
        rect.AnchorMax.Should().Be(new Vector2(1f, 1f));
        rect.Pivot.Should().Be(new Vector2(0.5f, 0.5f));
        rect.SizeDelta.Should().Be(Vector2.Zero, "a stretched element is as large as the room between its anchors");
    }

    [Fact]
    public void UiBuilder_AButton_RemembersWhatAPressDoes()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        var pressed = 0;
        ButtonHandle button = ui.Button("close").Text("Close").OnClick(() => pressed++);

        // The click is kept by the builder rather than by the button, and the service is what raises it when the frame's event arrives.
        ui.Clicks.Should().ContainKey(button.Entity);
        ui.Clicks[button.Entity]();

        pressed.Should().Be(1, "the handler that was written is the one that was kept");
    }

    [Fact]
    public void UiBuilder_AnImage_KeepsThePathAndTheTint()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity image = ui.Image("icon").Source("Textures/Items/sword.png").Tint(Color.Red).Entity;
        ImageComponent component = world.Get<ImageComponent>(image);

        component.TexturePath.Should().Be("Textures/Items/sword.png");
        component.Tint.Should().Be(Color.Red);
    }

    [Fact]
    public void UiBuilder_OrderAndVisible_AreWrittenIntoTheRectangle()
    {
        var world = new World();
        var ui = new UiBuilder(world, CreateCanvas(world));
        Entity label = ui.Label("title").Order(5).Visible(false).Entity;
        RectTransformComponent rect = world.Get<RectTransformComponent>(label);

        rect.ZOrder.Should().Be(5);
        rect.Visible.Should().BeFalse("a window that hides a piece of itself reads that back from the element");
    }

    private static Entity CreateCanvas(World world)
    {
        Entity canvas = world.CreateEntity();
        world.Set(canvas, new CanvasComponent
        {
            IsRoot = true,
            DesignSize = new Vector2(1280f, 720f),
            ScaleMode = CanvasScaleMode.ScaleWithScreenSize,
            Match = 0.5f,
        });
        return canvas;
    }
}