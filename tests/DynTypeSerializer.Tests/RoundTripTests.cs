using DynTypeSerializer.Tests.Models;
using Xunit;

namespace DynTypeSerializer.Tests;

/// <summary>
/// Round-trip fidelity tests: Serialize followed by Deserialize should
/// reconstruct the original value for supported types.
/// </summary>
public class RoundTripTests
{
    [Fact]
    public void RoundTrip_Int()
    {
        object value = 7;
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_String()
    {
        object value = "hello world";
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Bool()
    {
        object value = true;
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Double()
    {
        object value = 3.14159;
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Decimal()
    {
        object value = 123456789.123456789m;
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_DateTime()
    {
        object value = new DateTime(1999, 12, 31, 23, 59, 58);
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_DateTimeOffset()
    {
        object value = new DateTimeOffset(2022, 6, 15, 10, 0, 0, TimeSpan.FromHours(3));
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_TimeSpan()
    {
        object value = new TimeSpan(2, 3, 4, 5, 6);
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Guid()
    {
        object value = Guid.NewGuid();
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Char()
    {
        object value = 'Z';
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Uri()
    {
        object value = new Uri("https://example.com/path?q=1");
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Version()
    {
        object value = new Version(1, 2, 3, 4);
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_Enum()
    {
        object value = Color.Red;
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_NullableInt_HasValue()
    {
        int? value = 99;
        int? result = Serializer.Deserialize<int?>(Serializer.Serialize(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_NullableInt_Null()
    {
        int? value = null;
        // Deserialize<T> throws when the result is null; verify that behavior
        // for a null serialized value.
        string json = Serializer.Serialize((object?)value);
        Assert.Throws<InvalidOperationException>(() => Serializer.Deserialize<int?>(json));
    }

    [Fact]
    public void RoundTrip_ObjectArray()
    {
        object[] value = { 1, "two", 3.0, null, true };
        var result = Serializer.Deserialize<object[]>(Serializer.Serialize(value));
        Assert.NotNull(result);
        Assert.Equal(value.Length, result!.Length);
        Assert.Equal(1, result[0]);
        Assert.Equal("two", result[1]);
        Assert.Equal(3.0, result[2]);
        Assert.Null(result[3]);
        Assert.Equal(true, result[4]);
    }

    [Fact]
    public void RoundTrip_IntList()
    {
        var value = new List<int> { 1, 2, 3, 4 };
        var result = Serializer.Deserialize<List<int>>(Serializer.Serialize(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_StringListWithNulls()
    {
        var value = new List<string?> { "a", null, "c" };
        var result = Serializer.Deserialize<List<string?>>(Serializer.Serialize(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_StringDictionary()
    {
        var value = new Dictionary<string, int> { ["x"] = 10, ["y"] = 20 };
        var result = Serializer.Deserialize<Dictionary<string, int>>(Serializer.Serialize(value));
        Assert.Equal(value, result);
    }

    [Fact]
    public void RoundTrip_IntKeyedDictionary()
    {
        var value = new Dictionary<int, string> { [1] = "one", [2] = "two" };
        var result = Serializer.Deserialize<Dictionary<int, string>>(Serializer.Serialize(value));
        Assert.Equal(value, result);
    }

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

        var result = Serializer.Deserialize<Person>(Serializer.Serialize(person));
        Assert.NotNull(result);
        Assert.Equal(person.Name, result!.Name);
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
    public void RoundTrip_PolymorphicAsObject()
    {
        object value = new Dog { Name = "Rex", Legs = 4 };
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.IsType<Dog>(result);
        var dog = (Dog)result!;
        Assert.Equal("Rex", dog.Name);
        Assert.Equal(4, dog.Legs);
    }

    [Fact]
    public void RoundTrip_PolymorphicViaBaseType()
    {
        Animal value = new Cat { Name = "Mia", LivesNine = true };
        var json = Serializer.Serialize<Animal>(value);
        var result = Serializer.Deserialize<Animal>(json);
        Assert.IsType<Cat>(result);
        var cat = (Cat)result!;
        Assert.Equal("Mia", cat.Name);
        Assert.True(cat.LivesNine);
    }

    [Fact]
    public void RoundTrip_NestedComplexGraph()
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

        var result = Serializer.Deserialize<Garage>(Serializer.Serialize(garage));
        Assert.NotNull(result);
        Assert.Equal("Downtown", result!.Location);
        Assert.Equal(2, result.Cars.Count);
        Assert.Equal("Toyota", result.Cars[0].Make);
        Assert.Equal(2018, result.Cars[1].Year);
    }

    [Fact]
    public void RoundTrip_TypeProperty()
    {
        var holder = new TypeHolder { Type = typeof(string), Label = "text" };
        var result = Serializer.Deserialize<TypeHolder>(Serializer.Serialize(holder));
        Assert.NotNull(result);
        Assert.Equal(typeof(string), result!.Type);
        Assert.Equal("text", result.Label);
    }

    [Fact]
    public void RoundTrip_DateTimePrecision_RoundTripFormatPreserved()
    {
        // Round-trip "O" format retains sub-second precision.
        object value = new DateTime(2023, 1, 1, 12, 34, 56, 789);
        var result = Serializer.Deserialize<object>(Serializer.Serialize<object>(value));
        Assert.Equal(value, result);
    }
}
