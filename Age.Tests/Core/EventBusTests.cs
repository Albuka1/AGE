using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class EventBusTests
{
    [Fact]
    public void EventBus_Raise_QueuesTheEventUntilDispatch()
    {
        var bus = new EventBus();
        var received = new List<int>();
        bus.Subscribe<NumberEvent>((_, number) => received.Add(number.Value));

        bus.Raise(new NumberEvent(7));

        received.Should().BeEmpty("raising queues the event");

        bus.Dispatch().Should().Be(1);
        received.Should().Equal(7);
        bus.Dispatch().Should().Be(0, "the queue was drained");
    }

    [Fact]
    public void EventBus_DirectedRaise_CarriesTheEntityAndABroadcastCarriesTheDefaultOne()
    {
        var world = new World();
        Entity entity = world.CreateEntity();
        var seen = new List<Entity>();
        world.Events.Subscribe<NumberEvent>((source, _) => seen.Add(source));

        world.Events.Raise(entity, new NumberEvent(1));
        world.Events.Raise(new NumberEvent(2));
        world.Events.Dispatch();

        seen.Should().HaveCount(2);
        seen[0].Should().Be(entity);
        seen[1].Should().Be(default(Entity));
    }

    [Fact]
    public void EventBus_Unsubscribe_StopsTheDelivery()
    {
        var bus = new EventBus();
        int calls = 0;
        Action<Entity, NumberEvent> handler = (_, _) => calls++;

        bus.Subscribe(handler);
        bus.Raise(new NumberEvent(1));
        bus.Dispatch();

        bus.Unsubscribe(handler).Should().BeTrue();

        bus.Raise(new NumberEvent(2));
        bus.Dispatch();

        calls.Should().Be(1);
        bus.Unsubscribe(handler).Should().BeFalse("the handler is no longer subscribed");
    }

    [Fact]
    public void EventBus_HandlerThatUnsubscribes_DoesNotDisturbTheDeliveryOfTheOthers()
    {
        var bus = new EventBus();
        var calls = new List<string>();
        Action<Entity, NumberEvent> second = (_, _) => calls.Add("second");
        Action<Entity, NumberEvent> first = (_, _) =>
        {
            calls.Add("first");
            bus.Unsubscribe(second);
        };

        bus.Subscribe(first);
        bus.Subscribe(second);
        bus.Raise(new NumberEvent(1));
        bus.Dispatch();

        calls.Should().Equal("first", "second");

        bus.Raise(new NumberEvent(2));
        bus.Dispatch();

        calls.Should().Equal("first", "second", "first");
    }

    [Fact]
    public void EventBus_EventRaisedByAHandler_WaitsForTheNextDispatch()
    {
        var bus = new EventBus();
        int calls = 0;
        bus.Subscribe<NumberEvent>((_, number) =>
        {
            calls++;

            if (number.Value < 3)
            {
                bus.Raise(new NumberEvent(number.Value + 1));
            }
        });

        bus.Raise(new NumberEvent(1));

        bus.Dispatch().Should().Be(1, "only the event that was raised before the dispatch is delivered");
        calls.Should().Be(1);

        bus.Dispatch().Should().Be(1);
        calls.Should().Be(2);
    }

    [Fact]
    public void EventBus_SubscribeNull_Throws() =>
        FluentActions.Invoking(() => new EventBus().Subscribe<NumberEvent>(null!))
            .Should().Throw<ArgumentNullException>();

    private readonly record struct NumberEvent(int Value);
}
