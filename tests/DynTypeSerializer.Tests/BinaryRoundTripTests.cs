using DynTypeSerializer.Tests.Models;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>
/// Round-trip tests for the compact binary format via
/// <c>Serializer.Serialize</c> / <c>Deserialize&gt;T&lt;(byte[])</c>.
/// </summary>
public class BinaryRoundTripTests
{
    private static T RoundTrip<T>(T value)
    {
        var bytes = Serializer.SerializeToBytes(value);
        return Serializer.Deserialize<T>(bytes)!;
    }

    private static object? RoundTripBoxed<T>(T value)
    {
        var bytes = Serializer.SerializeToBytes<object>(value);
        return Serializer.Deserialize<object>(bytes);
    }

    // ── Primitives ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("a longer string with many chars")]
    public void RoundTrip_String(string? value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RoundTrip_Bool(bool value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void RoundTrip_Int(int value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void RoundTrip_Long(long value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData(0U)]
    [InlineData(uint.MaxValue)]
    public void RoundTrip_UInt(uint value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)255)]
    public void RoundTrip_Byte(byte value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)-12345)]
    public void RoundTrip_Short(short value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData(0.0f)]
    [InlineData(3.5f)]
    [InlineData(3.4028235E+38f)] // float.MaxValue
    public void RoundTrip_Float(float value)
        => Assert.Equal(value, RoundTrip(value));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-123.456)]
    [InlineData(1.7976931348623157E+308)] // double.MaxValue
    public void RoundTrip_Double(double value)
        => Assert.Equal(value, RoundTrip(value));

    [Fact]
    public void RoundTrip_Decimal()
    {
        Assert.Equal(0m, RoundTrip(0m));
        Assert.Equal(123.456m, RoundTrip(123.456m));
        Assert.Equal(-987654321.123m, RoundTrip(-987654321.123m));
    }

    [Fact]
    public void RoundTrip_Char()
        => Assert.Equal('Z', RoundTrip('Z'));

    [Fact]
    public void RoundTrip_Enum()
        => Assert.Equal(Color.Blue, RoundTrip(Color.Blue));

    [Fact]
    public void RoundTrip_Nullable_HasValue()
        => Assert.Equal(42, RoundTrip((int?)42));

    [Fact]
    public void RoundTrip_Nullable_Null()
        => Assert.Null(RoundTrip((int?)null));

    // ── Boxed (type-preserving) values ──────────────────────────────────────

    [Fact]
    public void RoundTrip_BoxedInt()
    {
        // Boxed numeric values fall back to the widest integral type (long),
        // mirroring the JSON object-fallback behavior for untagged numbers.
        var result = RoundTripBoxed(42);
        Assert.Equal(42L, result);
    }

    [Fact]
    public void RoundTrip_BoxedString()
        => Assert.Equal("hi", RoundTripBoxed("hi"));

    [Fact]
    public void RoundTrip_BoxedBool()
        => Assert.Equal(true, RoundTripBoxed(true));

    [Fact]
    public void RoundTrip_BoxedCharArray()
    {
        var arr = new object[] { 1, "two", true };
        var result = (object[])RoundTripBoxed(arr)!;
        Assert.Equal(1L, result[0]);
        Assert.Equal("two", result[1]);
        Assert.Equal(true, result[2]);
    }

    // ── Collections ─────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_IntArray()
    {
        int[] value = { 1, 2, 3, 1000 };
        Assert.Equal(value, RoundTrip(value));
    }

    [Fact]
    public void RoundTrip_StringList()
    {
        var value = new List<string> { "a", "bb", "hello" };
        Assert.Equal(value, RoundTrip(value));
    }

    [Fact]
    public void RoundTrip_ListWithNulls()
    {
        var value = new List<string?> { "a", null, "c" };
        Assert.Equal(value, RoundTrip(value));
    }

    [Fact]
    public void RoundTrip_EmptyList()
    {
        var value = new List<int>();
        Assert.Equal(value, RoundTrip(value));
    }

    [Fact]
    public void RoundTrip_IntDictionary()
    {
        var value = new Dictionary<int, string> { [1] = "one", [2] = "two" };
        Assert.Equal(value, RoundTrip(value));
    }

    [Fact]
    public void RoundTrip_NestedCollections()
    {
        var value = new List<List<int>> { new() { 1, 2 }, new() { 3 }, new() { } };
        Assert.Equal(value, RoundTrip(value));
    }

    // ── Objects ─────────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_ComplexModel()
    {
        var person = new Person
        {
            Name = "Alice",
            Age = 30,
            IsActive = true,
            Score = 98.5,
            Balance = 1234.56m,
            BirthDate = new DateTime(1990, 1, 2),
            Duration = new TimeSpan(0, 1, 2),
            Id = Guid.NewGuid(),
            Initial = 'A',
            Homepage = new Uri("https://alice.example"),
            Version = new Version(2, 0)
        };

        var result = RoundTrip(person);
        Assert.Equal(person.Name, result.Name);
        Assert.Equal(person.Age, result.Age);
        Assert.Equal(person.IsActive, result.IsActive);
        Assert.Equal(person.Score, result.Score);
        Assert.Equal(person.Balance, result.Balance);
        Assert.Equal(person.BirthDate, result.BirthDate);
        Assert.Equal(person.Duration, result.Duration);
        Assert.Equal(person.Id, result.Id);
        Assert.Equal(person.Initial, result.Initial);
        Assert.Equal(person.Homepage, result.Homepage);
        Assert.Equal(person.Version, result.Version);
    }

    [Fact]
    public void RoundTrip_NestedObjectGraph()
    {
        var garage = new Garage
        {
            Location = "Downtown",
            Cars = new List<Car>
            {
                new() { Make = "Toyota", Year = 2020 },
                new() { Make = "Ford", Year = 2018 }
            }
        };

        var result = RoundTrip(garage);
        Assert.Equal("Downtown", result.Location);
        Assert.Equal(2, result.Cars.Count);
        Assert.Equal("Toyota", result.Cars[0].Make);
        Assert.Equal(2018, result.Cars[1].Year);
    }

    [Fact]
    public void RoundTrip_ObjectWithEnumProperty()
    {
        var palette = new Palette { Favorite = Color.Green, NullableInt = 7, NullableBool = true, NullableDouble = 1.5 };
        var result = RoundTrip(palette);
        Assert.Equal(Color.Green, result.Favorite);
        Assert.Equal(7, result.NullableInt);
        Assert.Equal(true, result.NullableBool);
        Assert.Equal(1.5, result.NullableDouble);
    }

    // ── Binary format specifics ─────────────────────────────────────────────

    [Fact]
    public void Serialize_HasValidBinaryHeader()
    {
        var bytes = Serializer.SerializeToBytes(42);
        // Magic "DB"
        Assert.Equal((byte)'D', bytes[0]);
        Assert.Equal((byte)'B', bytes[1]);
        // Version 1
        Assert.Equal(1, bytes[2]);
    }

    [Fact]
    public void Binary_IsSmallerThanJson_ForRepeatedStructure()
    {
        // A list of records with repeated property names: binary eliminates the
        // repeated field names, so it should be smaller than the JSON form.
        var value = Enumerable.Range(0, 200)
            .Select(i => new Person { Name = "User" + i, Age = i, IsActive = i % 2 == 0 })
            .ToList();

        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(Serializer.SerializeToString(value));
        var binary = Serializer.SerializeToBytes(value);
        Assert.True(binary.Length < jsonBytes.Length, $"binary {binary.Length} should be < json {jsonBytes.Length}");
    }
}
