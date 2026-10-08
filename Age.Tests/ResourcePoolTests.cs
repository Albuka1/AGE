using Age.Core;
using FluentAssertions;
using Xunit;

namespace Age.Tests;

public sealed class ResourcePoolTests
{
    [Fact]
    public void ResourcePool_Add_ReturnsValidHandle()
    {
        var pool = new ResourcePool<string>();

        ResourceHandle handle = pool.Add("texture");

        handle.IsValid.Should().BeTrue();
        pool.Count.Should().Be(1);
        pool.TryGet(handle, out string? value).Should().BeTrue();
        value.Should().Be("texture");
    }

    [Fact]
    public void ResourcePool_DefaultHandle_IsInvalidAndNotFound()
    {
        var pool = new ResourcePool<string>();

        ResourceHandle handle = default;

        handle.IsValid.Should().BeFalse();
        pool.TryGet(handle, out _).Should().BeFalse();
    }

    [Fact]
    public void ResourcePool_HandleFromAnotherPool_IsNotFound()
    {
        var first = new ResourcePool<string>();
        var second = new ResourcePool<string>();
        ResourceHandle handle = first.Add("texture");

        second.TryGet(handle, out _).Should().BeFalse();
        second.Release(handle).Should().BeFalse();
        first.TryGet(handle, out string? value).Should().BeTrue();
        value.Should().Be("texture");
    }

    [Fact]
    public void ResourcePool_TryGetHandle_ResolvesRegisteredPath()
    {
        var pool = new ResourcePool<string>();
        ResourceHandle handle = pool.Add("texture", "art/player.png");

        pool.TryGetHandle("art/player.png", out ResourceHandle found).Should().BeTrue();
        found.Should().Be(handle);
        pool.TryGetHandle("art/missing.png", out _).Should().BeFalse();
    }

    [Fact]
    public void ResourcePool_AddWithRegisteredPath_ThrowsInvalidOperationException()
    {
        var pool = new ResourcePool<string>();
        pool.Add("first", "art/player.png");

        Action act = () => pool.Add("second", "art/player.png");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResourcePool_Release_RemovesResourceAndPath()
    {
        var pool = new ResourcePool<string>();
        ResourceHandle handle = pool.Add("texture", "art/player.png");

        bool released = pool.Release(handle);

        released.Should().BeTrue();
        pool.Count.Should().Be(0);
        pool.TryGet(handle, out _).Should().BeFalse();
        pool.TryGetHandle("art/player.png", out _).Should().BeFalse();
        pool.Release(handle).Should().BeFalse();
    }

    [Fact]
    public void ResourcePool_ReusedSlot_KeepsStaleHandleInvalid()
    {
        var pool = new ResourcePool<string>();
        ResourceHandle stale = pool.Add("first");
        pool.Release(stale);

        ResourceHandle current = pool.Add("second");

        current.Id.Should().Be(stale.Id);
        current.Generation.Should().NotBe(stale.Generation);
        pool.TryGet(stale, out _).Should().BeFalse();
        pool.TryGet(current, out string? value).Should().BeTrue();
        value.Should().Be("second");
    }

    [Fact]
    public void ResourcePool_MoreResourcesThanSlots_GrowsAndResolvesAll()
    {
        var pool = new ResourcePool<string>();
        var handles = new List<ResourceHandle>();

        for (int index = 0; index < 20; index++)
        {
            handles.Add(pool.Add("value" + index));
        }

        pool.Count.Should().Be(20);

        for (int index = 0; index < handles.Count; index++)
        {
            pool.TryGet(handles[index], out string? value).Should().BeTrue();
            value.Should().Be("value" + index);
        }
    }

    [Fact]
    public void ResourcePool_Clear_ReleasesEveryResource()
    {
        var pool = new ResourcePool<string>();
        pool.Add("first");
        pool.Add("second");
        var released = new List<string>();

        pool.Clear(released.Add);

        released.Should().Equal("first", "second");
        pool.Count.Should().Be(0);
    }

    [Fact]
    public void ResourcePool_ClearWhenReleaseThrows_KeepsPoolConsistent()
    {
        var pool = new ResourcePool<string>();
        pool.Add("first", "first.txt");
        pool.Add("second", "second.txt");

        Action act = () => pool.Clear(value => throw new InvalidOperationException(value));

        act.Should().Throw<InvalidOperationException>();
        pool.Count.Should().Be(1);
        pool.TryGetHandle("first.txt", out _).Should().BeFalse();
        pool.TryGetHandle("second.txt", out _).Should().BeTrue();
    }

    [Fact]
    public void ResourcePool_GetHandles_ReturnsEveryLiveHandleInSlotOrder()
    {
        var pool = new ResourcePool<string>();
        ResourceHandle first = pool.Add("first", "first.txt");
        ResourceHandle released = pool.Add("second");
        pool.Add("third", "third.txt");
        pool.Release(released);

        ResourceHandle[] handles = pool.GetHandles();

        handles.Should().HaveCount(2);
        handles[0].Should().Be(first);
        handles[1].Should().NotBe(released);
        pool.TryGet(handles[1], out string? value).Should().BeTrue();
        value.Should().Be("third");
        new ResourcePool<string>().GetHandles().Should().BeEmpty();
    }

    [Fact]
    public void ResourcePool_Clear_KeepsStaleHandleInvalid()
    {
        var pool = new ResourcePool<string>();
        ResourceHandle stale = pool.Add("first");
        pool.Clear();

        ResourceHandle current = pool.Add("second");

        current.Id.Should().Be(stale.Id);
        pool.TryGet(stale, out _).Should().BeFalse();
    }

    [Fact]
    public void ResourcePool_ClearWithACallbackThatAdds_KeepsTheNewResource()
    {
        var pool = new ResourcePool<string>();
        pool.Add("first", "first.txt");
        ResourceHandle? added = null;

        pool.Clear(value =>
        {
            if (value == "first")
            {
                added = pool.Add("second", "second.txt");
            }
        });

        pool.Count.Should().Be(1);
        added.Should().NotBeNull();
        pool.TryGet(added!.Value, out string? resource).Should().BeTrue();
        resource.Should().Be("second");
    }

    [Fact]
    public void ResourcePool_ParallelCalls_KeepThePoolConsistent()
    {
        var pool = new ResourcePool<int>();
        int failures = 0;

        Parallel.For(0, 16, index =>
        {
            for (int round = 0; round < 200; round++)
            {
                string path = $"art/{index}-{round}.png";
                ResourceHandle handle = pool.Add(round, path);

                if (!pool.TryGet(handle, out int value) || value != round)
                {
                    Interlocked.Increment(ref failures);
                }

                if (!pool.TryGetHandle(path, out ResourceHandle found) || found != handle)
                {
                    Interlocked.Increment(ref failures);
                }

                if (pool.GetHandles().Length == 0)
                {
                    Interlocked.Increment(ref failures);
                }

                if (!pool.Release(handle))
                {
                    Interlocked.Increment(ref failures);
                }
            }
        });

        failures.Should().Be(0);
        pool.Count.Should().Be(0);
    }
}
