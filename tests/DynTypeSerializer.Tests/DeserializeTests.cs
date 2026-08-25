using DynTypeSerializer.Tests.Models;
using System.Text.Json;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>Tests for the Deserialize and DeserializeDynamic methods.</summary>
public class DeserializeTests
{
    [Fact]
    public void Deserialize_PlainInt_ReturnsValue()
    {
        var result = Serializer.Deserialize<int>("42");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Deserialize_PlainString_ReturnsValue()
    {
        var result = Serializer.Deserialize<string>("\"hello\"");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Deserialize_NullString_ReturnsDefault()
    {
        // A JSON null payload deserializes to null/default, not an exception.
        var result = Serializer.Deserialize<string?>("null");
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_TaggedInt_RestoresType()
    {
        var result = Serializer.Deserialize<object>("{\"$t\":\"i\",\"$v\":42}");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Deserialize_TaggedString_RestoresType()
    {
        var result = Serializer.Deserialize<object>("{\"$t\":\"s\",\"$v\":\"hey\"}");
        Assert.Equal("hey", result);
    }

    [Fact]
    public void Deserialize_TaggedBool_RestoresType()
    {
        var result = Serializer.Deserialize<object>("{\"$t\":\"b\",\"$v\":false}");
        Assert.Equal(false, result);
    }

    [Fact]
    public void Deserialize_TaggedDouble_RestoresType()
    {
        var result = Serializer.Deserialize<object>("{\"$t\":\"d\",\"$v\":12.5}");
        Assert.Equal(12.5, result);
    }

    [Fact]
    public void Deserialize_TaggedDateTime_RestoresValue()
    {
        var dt = new DateTime(2021, 3, 4, 5, 6, 7);
        string json = Serializer.SerializeToString<object>(dt);
        var result = Serializer.Deserialize<object>(json);
        Assert.Equal(dt, result);
    }

    [Fact]
    public void Deserialize_TaggedGuid_RestoresValue()
    {
        var guid = Guid.NewGuid();
        string json = Serializer.SerializeToString<object>(guid);
        var result = Serializer.Deserialize<object>(json);
        Assert.Equal(guid, result);
    }

    [Fact]
    public void Deserialize_TaggedEnum_RestoresEnum()
    {
        var enumJson = Serializer.SerializeToString<object>(Color.Blue);
        var result = Serializer.Deserialize<object>(enumJson);
        Assert.Equal(Color.Blue, result);
    }

    [Fact]
    public void Deserialize_ComplexObjectAsync()
    {
        string json = "{\"Name\":\"Alice\",\"Age\":30,\"IsActive\":true,\"Score\":5.5}";
        var person = Serializer.Deserialize<Person>(json);
        Assert.NotNull(person);
        Assert.Equal("Alice", person!.Name);
        Assert.Equal(30, person.Age);
        Assert.True(person.IsActive);
    }

    [Fact]
    public void Deserialize_IntoObjectWithMissingProperties_LeftAtDefault()
    {
        string json = "{\"Name\":\"Bob\"}";
        var person = Serializer.Deserialize<Person>(json);
        Assert.NotNull(person);
        Assert.Equal("Bob", person!.Name);
        Assert.Equal(0, person.Age);
    }

    [Fact]
    public void Deserialize_ListOfPrimitives()
    {
        string json = "[1,2,3]";
        var list = Serializer.Deserialize<List<int>>(json);
        Assert.NotNull(list);
        Assert.Equal(new List<int> { 1, 2, 3 }, list);
    }

    [Fact]
    public void Deserialize_StringArray()
    {
        string json = "[\"a\",\"b\"]";
        var arr = Serializer.Deserialize<string[]>(json);
        Assert.NotNull(arr);
        Assert.Equal(new[] { "a", "b" }, arr);
    }

    [Fact]
    public void Deserialize_DictionaryStringInt()
    {
        string json = "{\"a\":1,\"b\":2}";
        var dict = Serializer.Deserialize<Dictionary<string, int>>(json);
        Assert.NotNull(dict);
        Assert.Equal(1, dict!["a"]);
        Assert.Equal(2, dict["b"]);
    }

    [Fact]
    public void Deserialize_DynamicReturnsDictionaryForPlainObject()
    {
        var result = Serializer.DeserializeDynamic("{\"a\":1}");
        Assert.IsType<Dictionary<string, object?>>(result);
        var dict = (Dictionary<string, object?>)result!;
        Assert.True(dict.ContainsKey("a"));
    }

    [Fact]
    public void DeserializeDynamic_PlainNumber_StaysNumeric()
    {
        var result = Serializer.DeserializeDynamic("{\"a\":1,\"b\":1.5,\"c\":true}");
        var dict = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal(1L, dict["a"]);          // integer stays integral
        Assert.Equal(1.5, dict["b"]);         // decimal stays a double
        Assert.Equal(true, dict["c"]);        // bool stays bool
    }

    [Fact]
    public void DeserializeDynamic_TaggedValue_RestoresBoxedType()
    {
        string json = Serializer.SerializeToString<object>(42);
        var result = Serializer.DeserializeDynamic(json);
        Assert.Equal(42, result);
    }

    [Fact]
    public void Deserialize_WithRootTag_DeserializesValueOnly()
    {
        string json = "{\"$r\":\"DynTypeSerializer.Tests.Models.Person\",\"$v\":{\"Name\":\"Zed\"}}";
        var person = Serializer.Deserialize<Person>(json);
        Assert.NotNull(person);
        Assert.Equal("Zed", person!.Name);
    }

    [Fact]
    public void Deserialize_UnknownType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Serializer.Deserialize<object>("{\"$t\":\"X.Y.NotARealType\",\"$v\":1}"));
    }

    [Fact]
    public void Deserialize_MalformedJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => Serializer.Deserialize<object>("not json at all"));
    }

    [Fact]
    public void Deserialize_ReadOnlyProperty_IgnoredOnRead()
    {
        string json = Serializer.SerializeToString(new ReadOnlyModel { Id = 9 });
        var model = Serializer.Deserialize<ReadOnlyModel>(json);
        Assert.NotNull(model);
        Assert.Equal(9, model!.Id);
        Assert.Equal("Id-9", model.Computed);
    }
}
