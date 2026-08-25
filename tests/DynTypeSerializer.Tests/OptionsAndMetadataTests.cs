using DynTypeSerializer.Tests.Models;
using System.Text.Json;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>
/// Tests for the <see cref="Serializer.Options"/> and the root-type metadata
/// helpers (<c>ContainsRootType</c>, <c>GetRootType</c>).
/// </summary>
public class OptionsAndMetadataTests
{
    [Fact]
    public void ContainsRootType_WithRootTag_ReturnsTrue()
    {
        string json = Serializer.Serialize(new Person(), new Serializer.Options { IncludeRootType = true });
        Assert.True(Serializer.ContainsRootType(json));
    }

    [Fact]
    public void ContainsRootType_WithoutRootTag_ReturnsFalse()
    {
        string json = Serializer.Serialize(new Person());
        Assert.False(Serializer.ContainsRootType(json));
    }

    [Fact]
    public void ContainsRootType_OnPrimitiveJson_ReturnsFalse()
    {
        Assert.False(Serializer.ContainsRootType("42"));
        Assert.False(Serializer.ContainsRootType("\"str\""));
        Assert.False(Serializer.ContainsRootType("[1,2]"));
    }

    [Fact]
    public void ContainsRootType_OnNull_ReturnsFalse()
    {
        Assert.False(Serializer.ContainsRootType("null"));
    }

    [Fact]
    public void GetRootType_WithRootTag_ReturnsType()
    {
        string json = Serializer.Serialize(new Person(), new Serializer.Options { IncludeRootType = true });
        Type? rootType = Serializer.GetRootType(json);
        Assert.Equal(typeof(Person), rootType);
    }

    [Fact]
    public void GetRootType_WithoutRootTag_ReturnsObject()
    {
        string json = Serializer.Serialize(new Person());
        Type? rootType = Serializer.GetRootType(json);
        Assert.Equal(typeof(object), rootType);
    }

    [Fact]
    public void GetRootType_InvalidJson_Throws()
    {
        // Malformed JSON is no longer swallowed; it surfaces as an exception.
        Assert.ThrowsAny<Exception>(() => Serializer.GetRootType("garbage"));
    }

    [Fact]
    public void IncludeRootType_WrapsOutputInEnvelope()
    {
        var person = new Person { Name = "Ada" };
        string json = Serializer.Serialize(person, new Serializer.Options { IncludeRootType = true });
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("$r", out var r));
        Assert.True(doc.RootElement.TryGetProperty("$v", out _));
        Assert.Equal(typeof(Person).FullName, r.GetString());
    }

    [Fact]
    public void WriteIndented_ProducesMultilineJson()
    {
        string compact = Serializer.Serialize(new Person { Name = "x" });
        string indented = Serializer.Serialize(new Person { Name = "x" }, new Serializer.Options { WriteIndented = true });
        Assert.DoesNotContain("\n", compact);
        Assert.Contains("\n", indented);
    }

    [Fact]
    public void IncludeFullAssemblyInfo_UsesAssemblyQualifiedName()
    {
        object value = new Person { Name = "Ada" };
        string json = Serializer.Serialize<object>(value, new Serializer.Options { IncludeFullAssemblyInfo = true });
        // Full assembly-qualified name includes a comma + assembly.
        Assert.Contains(typeof(Person).AssemblyQualifiedName!, json);
    }

    [Fact]
    public void IncludeFullAssemblyInfo_StillRoundTrips()
    {
        object value = new Dog { Name = "Rex", Legs = 4 };
        string json = Serializer.Serialize<object>(value, new Serializer.Options { IncludeFullAssemblyInfo = true });
        var result = Serializer.Deserialize<object>(json);
        Assert.IsType<Dog>(result);
    }

    [Fact]
    public void RootTypeTag_DoesNotAffectDeserializeOfTypedValue()
    {
        var person = new Person { Name = "Grace", Age = 42 };
        string json = Serializer.Serialize(person, new Serializer.Options { IncludeRootType = true });
        var result = Serializer.Deserialize<Person>(json);
        Assert.NotNull(result);
        Assert.Equal("Grace", result!.Name);
        Assert.Equal(42, result.Age);
    }

    [Fact]
    public void DefaultOptions_MatchDocumentedDefaults()
    {
        var options = new Serializer.Options();
        Assert.False(options.IncludeRootType);
        Assert.False(options.IncludeFullAssemblyInfo);
        Assert.False(options.WriteIndented);
    }
}
