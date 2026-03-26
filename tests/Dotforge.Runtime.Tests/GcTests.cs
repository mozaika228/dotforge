using Dotforge.Runtime.Gc;
using System.Reflection.Metadata;

namespace Dotforge.Runtime.Tests;

public sealed class GcTests
{
    [Xunit.Fact]
    public void MinorCollectionPromotesGen0ToGen1()
    {
        var heap = new GenerationalHeap();
        var obj = heap.AllocateObject(default(TypeDefinitionHandle), "T", ["F"]);
        Xunit.Assert.Equal(0, obj.Generation);

        heap.CollectMinor([obj]);
        Xunit.Assert.Equal(1, obj.Generation);
        var stats = heap.GetStats();
        Xunit.Assert.Equal(1, stats.MinorCollections);
        Xunit.Assert.Equal(1, stats.Gen1Count);
    }

    [Xunit.Fact]
    public void WeakHandleIsClearedAfterCollection()
    {
        var heap = new GenerationalHeap();
        var obj = heap.AllocateObject(default(TypeDefinitionHandle), "T", []);
        var weak = heap.CreateHandle(obj, GcHandleKind.Weak);
        Xunit.Assert.NotNull(heap.ResolveHandle(weak));

        heap.CollectMajor([]);
        Xunit.Assert.Null(heap.ResolveHandle(weak));
    }

    [Xunit.Fact]
    public void FinalizerCallbackIsInvokedOnCollection()
    {
        var heap = new GenerationalHeap();
        var obj = heap.AllocateObject(default(TypeDefinitionHandle), "T", []);
        var invoked = false;
        heap.RegisterFinalizer(obj, _ => invoked = true);

        heap.CollectMajor([]);
        Xunit.Assert.True(invoked);
    }
}
