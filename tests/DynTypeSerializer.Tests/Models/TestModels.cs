namespace DynTypeSerializer.Tests.Models;

/// <summary>Test model with assorted primitive/wrapper properties.</summary>
public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public double Score { get; set; }
    public decimal Balance { get; set; }
    public DateTime BirthDate { get; set; }
    public TimeSpan Duration { get; set; }
    public Guid Id { get; set; }
    public char Initial { get; set; }
    public Uri? Homepage { get; set; }
    public Version? Version { get; set; }
}

/// <summary>Test enumeration.</summary>
public enum Color
{
    Red,
    Green,
    Blue
}

/// <summary>Test model that references an enum and nullable values.</summary>
public class Palette
{
    public Color Favorite { get; set; }
    public int? NullableInt { get; set; }
    public bool? NullableBool { get; set; }
    public double? NullableDouble { get; set; }
}

/// <summary>Polymorphic base class.</summary>
public abstract class Animal
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>Concrete derived type.</summary>
public class Dog : Animal
{
    public int Legs { get; set; }
}

/// <summary>Another concrete derived type.</summary>
public class Cat : Animal
{
    public bool LivesNine { get; set; }
}

/// <summary>Model with a read-only property (no setter) to exercise skip behavior.</summary>
public class ReadOnlyModel
{
    public int Id { get; set; }
    public string Computed => $"Id-{Id}";
}

/// <summary>Model with a <see cref="Type"/> property (special-cased in serialization).</summary>
public class TypeHolder
{
    public Type? Type { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Model that serializes a nested complex object.</summary>
public class Garage
{
    public string Location { get; set; } = string.Empty;
    public List<Car> Cars { get; set; } = new();
}

/// <summary>Nested model used by <see cref="Garage"/>.</summary>
public class Car
{
    public string Make { get; set; } = string.Empty;
    public int Year { get; set; }
}

/// <summary>A node that can point to itself, forming a circular reference.</summary>
public class Node
{
    public string Name { get; set; } = string.Empty;
    public Node? Next { get; set; }
}

/// <summary>Two objects that reference each other, forming a cycle.</summary>
public class A
{
    public string Value { get; set; } = string.Empty;
    public B? B { get; set; }
}

public class B
{
    public string Value { get; set; } = string.Empty;
    public A? A { get; set; }
}

/// <summary>A list that contains a reference to itself.</summary>
public class SelfContainingList
{
    public List<object> Items { get; set; } = new();
}

/// <summary>A deep chain used to exercise the depth guard.</summary>
public class ChainLink
{
    public string Id { get; set; } = string.Empty;
    public ChainLink? Next { get; set; }
}
