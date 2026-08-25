using DynTypeSerializer.Tests.Models;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>
/// Tests for the serializer's circular-reference detection and depth guard.
/// </summary>
public class CircularReferenceTests
{
    [Fact]
    public void Serialize_SelfReferencingNode_Throws()
    {
        var node = new Node { Name = "root" };
        node.Next = node; // cycle back to itself

        var ex = Assert.Throws<InvalidOperationException>(() => { Serializer.SerializeToBytes(node); });
        Assert.Contains("circular", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_MutualReferences_Throws()
    {
        var a = new A { Value = "a" };
        var b = new B { Value = "b" };
        a.B = b;
        b.A = a;

        var ex = Assert.Throws<InvalidOperationException>(() => { Serializer.SerializeToBytes(a); });
        Assert.Contains("circular", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_SelfContainingList_Throws()
    {
        var list = new SelfContainingList();
        list.Items.Add(list); // list contains itself

        var ex = Assert.Throws<InvalidOperationException>(() => { Serializer.SerializeToBytes(list); });
        Assert.Contains("circular", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_AcyclicChain_DoesNotThrow()
    {
        var node1 = new Node { Name = "1" };
        var node2 = new Node { Name = "2" };
        var node3 = new Node { Name = "3" };
        node1.Next = node2;
        node2.Next = node3;

        // Acyclic graph should serialize without a circular-reference error.
        var json = Serializer.SerializeToString(node1);
        Assert.Contains("\"Name\":\"1\"", json);
        Assert.Contains("\"Name\":\"2\"", json);
        Assert.Contains("\"Name\":\"3\"", json);
    }

    [Fact]
    public void Serialize_SharedReference_DoesNotThrow()
    {
        // The same object referenced from two independent branches is NOT a
        // cycle, so it should serialize fine.
        var shared = new Node { Name = "shared" };
        var parent = new SelfContainingList();
        parent.Items.Add(shared);
        parent.Items.Add(new { Link = shared });

        var json = Serializer.SerializeToString(parent);
        Assert.Contains("shared", json);
    }

    [Fact]
    public void Serialize_DeepGraph_ThrowsDepthExceeded()
    {
        // Build a chain deep enough to exceed the serialization depth guard.
        var head = new ChainLink { Id = "0" };
        var current = head;
        for (int i = 1; i < 5000; i++)
        {
            current.Next = new ChainLink { Id = i.ToString() };
            current = current.Next;
        }

        // A deep non-cyclic chain triggers the depth guard rather than a stack
        // overflow.
        var ex = Assert.Throws<InvalidOperationException>(() => { Serializer.SerializeToBytes(head); });
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_ConfigurableDepthLimit_IsHonored()
    {
        // Build a moderately deep chain (60 levels) that is fine within the
        // default limit but exceeds a user-configured limit of 10.
        var head = new ChainLink { Id = "0" };
        var current = head;
        for (int i = 1; i < 60; i++)
        {
            current.Next = new ChainLink { Id = i.ToString() };
            current = current.Next;
        }

        // Default limit (512) allows it.
        _ = Serializer.SerializeToBytes(head);

        // A custom low limit rejects it.
        var options = new Serializer.Options { MaxSerializationDepth = 10 };
        var ex = Assert.Throws<InvalidOperationException>(() => { Serializer.SerializeToBytes(head, options); });
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
