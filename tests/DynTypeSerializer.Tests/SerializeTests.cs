using DynTypeSerializer.Tests.Models;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>Tests for the Serialize methods and their JSON output shape.</summary>
public class SerializeTests
{
    [Fact]
    public void Serialize_Null_ReturnsNullLiteral()
    {
        string json = Serializer.Serialize((object?)null);
        Assert.Equal("null", json);
    }

    [Fact]
    public void Serialize_NullGeneric_ReturnsNullLiteral()
    {
        string json = Serializer.Serialize<string?>(null);
        Assert.Equal("null", json);
    }

    [Fact]
    public void Serialize_Int_NoTypeTag()
    {
        string json = Serializer.Serialize(42);
        Assert.Equal("42", json);
    }

    [Fact]
    public void Serialize_String_NoTypeTag()
    {
        string json = Serializer.Serialize("hello");
        Assert.Equal("\"hello\"", json);
    }

    [Fact]
    public void Serialize_ObjectBoxedInt_AddsTypeTag()
    {
        object value = 42;
        // Serialize<object> forces an 'object' declared type so a $t tag is emitted.
        string json = Serializer.Serialize<object>(value);
        Assert.Contains("\"$t\":\"i\"", json);
        Assert.Contains("\"$v\":42", json);
    }

    [Fact]
    public void Serialize_BoxedBool_AddsBoolTag()
    {
        object value = true;
        string json = Serializer.Serialize<object>(value);
        Assert.Contains("\"$t\":\"b\"", json);
        Assert.Contains("\"$v\":true", json);
    }

    [Fact]
    public void Serialize_BoxedDouble_AddsDoubleTag()
    {
        object value = 99.5;
        string json = Serializer.Serialize<object>(value);
        Assert.Contains("\"$t\":\"d\"", json);
        Assert.Contains("\"$v\":99.5", json);
    }

    [Fact]
    public void Serialize_BoxedCharacter_EncodesAsString()
    {
        object value = 'A';
        string json = Serializer.Serialize<object>(value);
        Assert.Contains("\"$t\":\"c\"", json);
        Assert.Contains("\"$v\":\"A\"", json);
    }

    [Fact]
    public void Serialize_DateTime_EncodesRoundTrippable()
    {
        var dt = new DateTime(2020, 5, 17, 11, 45, 0);
        string json = Serializer.Serialize<object>(dt);
        Assert.Contains("\"$t\":\"dt\"", json);
        // 'O' format round-trip
        Assert.Contains($"\"$v\":\"{dt:O}\"", json);
    }

    [Fact]
    public void Serialize_TimeSpan_EncodesConstantFormat()
    {
        var ts = new TimeSpan(1, 2, 3, 4);
        string json = Serializer.Serialize<object>(ts);
        Assert.Contains("\"$t\":\"ts\"", json);
        // 'c' constant format
        Assert.Contains($"\"$v\":\"{ts:c}\"", json);
    }

    [Fact]
    public void Serialize_Guid_EncodesAsString()
    {
        var guid = Guid.NewGuid();
        string json = Serializer.Serialize<object>(guid);
        Assert.Contains("\"$t\":\"g\"", json);
        Assert.Contains($"\"$v\":\"{guid}\"", json);
    }

    [Fact]
    public void Serialize_Decimal_NoPrecisionLoss()
    {
        object value = 123.456789m;
        string json = Serializer.Serialize<object>(value);
        // decimal serialized as string to avoid float precision loss
        Assert.Contains($"\"$v\":\"{value}\"", json);
    }

    [Fact]
    public void Serialize_Enum_EncodesAsName()
    {
        object value = Color.Green;
        string json = Serializer.Serialize<object>(value);
        Assert.Contains("\"$v\":\"Green\"", json);
    }

    [Fact]
    public void Serialize_ArrayOfObjects_EachElementTagged()
    {
        object[] value = { 1, "two", true };
        string json = Serializer.Serialize(value);
        Assert.StartsWith("[", json);
        Assert.Contains("\"$t\":\"i\"", json);
        Assert.Contains("\"$t\":\"s\"", json);
        Assert.Contains("\"$t\":\"b\"", json);
    }

    [Fact]
    public void Serialize_StringKeyedDictionary_ProducesObject()
    {
        var value = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        string json = Serializer.Serialize(value);
        Assert.StartsWith("{", json);
        Assert.Contains("\"a\"", json);
        Assert.Contains("\"b\"", json);
    }

    [Fact]
    public void Serialize_IntKeyedDictionary_ProducesKeyValueArray()
    {
        var value = new Dictionary<int, string> { [1] = "one", [2] = "two" };
        string json = Serializer.Serialize(value);
        Assert.StartsWith("[", json);
        Assert.Contains("\"$k\"", json);
        Assert.Contains("\"$v\"", json);
    }

    [Fact]
    public void Serialize_ComplexType_WithoutTagWhenDeclaredTypeKnown()
    {
        var person = new Person { Name = "Alice", Age = 30 };
        string json = Serializer.Serialize(person);
        // Declared type known -> no $t at root
        Assert.Contains("\"Name\":\"Alice\"", json);
        Assert.Contains("\"Age\":30", json);
        Assert.DoesNotContain("\"$t\"", json);
    }

    [Fact]
    public void Serialize_BoxedComplexType_AddsTypeTag()
    {
        object person = new Person { Name = "Bob", Age = 40 };
        string json = Serializer.Serialize<object>(person);
        // Declared type is object -> $t tag present with full type name
        Assert.Contains("\"$t\":\"DynTypeSerializer.Tests.Models.Person\"", json);
    }

    [Fact]
    public void Serialize_GenericWithMatchingDeclaredType_NoTag()
    {
        var person = new Person { Name = "Alice", Age = 30 };
        string json = Serializer.Serialize<Person>(person);
        Assert.DoesNotContain("\"$t\"", json);
    }

    [Fact]
    public void Serialize_PolymorphicAssignment_AddsDerivedTag()
    {
        Animal animal = new Dog { Name = "Rex", Legs = 4 };
        string json = Serializer.Serialize<Animal>(animal);
        Assert.Contains("\"$t\":\"DynTypeSerializer.Tests.Models.Dog\"", json);
    }

    [Fact]
    public void Serialize_ReadOnlyProperty_StillSerialized()
    {
        var model = new ReadOnlyModel { Id = 7 };
        string json = Serializer.Serialize(model);
        Assert.Contains("\"Computed\":\"Id-7\"", json);
    }

    [Fact]
    public void Serialize_TypeProperty_EncodesTypeName()
    {
        var holder = new TypeHolder { Type = typeof(string), Label = "x" };
        string json = Serializer.Serialize(holder);
        Assert.Contains($"\"Type\":\"{typeof(string).FullName}\"", json);
    }

    [Fact]
    public void Serialize_EmptyObject_ProducesEmptyBraces()
    {
        var obj = new { };
        string json = Serializer.Serialize(obj);
        Assert.Equal("{}", json);
    }

    [Fact]
    public void Serialize_AnonymousObject_PlainJson()
    {
        var obj = new { Name = "x", Count = 5 };
        string json = Serializer.Serialize(obj);
        Assert.Contains("\"Name\":\"x\"", json);
        Assert.Contains("\"Count\":5", json);
    }

    [Fact]
    public void Serialize_WriteIndented_FormatsOutput()
    {
        var person = new Person { Name = "Alice", Age = 30 };
        string json = Serializer.Serialize(person, new Serializer.Options { WriteIndented = true });
        Assert.Contains("\n", json);
    }
}
