# DynTypeSerializer

A fully dynamic, type-preserving JSON serializer for .NET, built on top of
`System.Text.Json`.

Unlike the standard `System.Text.Json` serializer — which requires you to know
the type at deserialization time and loses runtime type information for
polymorphic values — **DynTypeSerializer embeds type metadata into the JSON
itself**, so values can be round-tripped with full fidelity:

```csharp
Deserialize<T>(Serialize(x)) // ≈ x  for all supported types
```

It is particularly useful for logging, message-passing, dynamic object
persistence, and any scenario where the actual runtime type of an `object`,
interface, or abstract member must survive serialization.

## Features

- **Runtime type preservation** — every value carries the information needed
  to reconstruct its exact runtime type.
- **Round-trip fidelity** — `Serialize` → `Deserialize<T>` reconstructs the
  original object graph.
- **No attributes, no code generation, no source generators** — works with any
  public type, including third-party types you cannot modify.
- **Compact type tags** — built-in and well-known value types use short codes
  (e.g. `"i"` for `int`, `"s"` for `string`).
- **Nullable, dictionaries, and collections supported** — including
  non-string-keyed dictionaries and boxed collections.
- **Dynamic root deserialization** — deserialize JSON when you do not know the
  root type at compile time.
- **Native AOT compatible** (`<IsAotCompatible>true</IsAotCompatible>`).
- **Configurable** formatting, root-type embedding, and full assembly info.
- **Optional logging** through `Microsoft.Extensions.Logging.Abstractions`.

## Requirements

- .NET 10.0 or later
- `Microsoft.Extensions.Logging.Abstractions` (pulled in automatically via NuGet)

## Installation

Reference the project or the built NuGet package:

```
dotnet add package DynTypeSerializer
```

Or build the package locally (see [Building](#building)).

## Quick start

```csharp
using DynTypeSerializer;

object value = new List<object> { 42, "hello", null, new { Flag = true } };

// Serialize, preserving runtime types
string json = Serializer.Serialize(value);

// Deserialize back to the original type
var restored = Serializer.Deserialize<List<object>>(json);
```

The produced JSON embeds type tags on values whose runtime type differs from
the declared type:

```json
{
  "Name": "Alice",
  "Age": 30,
  "Items": [
    { "$t": "i", "$v": 42 },
    { "$t": "s", "$v": "hello" },
    null,
    {
      "$t": "oa",
      "$v": [
        { "$t": "s", "$v": "nested" },
        { "$t": "i", "$v": 123 },
        null
      ]
    }
  ],
  "Flags": {
    "IsActive": { "$t": "b", "$v": true },
    "Score": { "$t": "d", "$v": 99.5 }
  },
  "Test": "03:30:00",
  "Sub": null
}
```

## API

All methods are on the static class `Serializer`.

| Method | Description |
| --- | --- |
| `Serialize(object? obj, Options? options = null)` | Serializes any object to a JSON string, preserving the runtime type. |
| `Serialize<T>(T obj, Options? options = null)` | Serializes with a known declared type; suppresses the `$t` tag when the runtime type matches `T`. |
| `Deserialize<T>(string json)` | Deserializes JSON back to `T`, restoring all dynamic types. |
| `DeserializeDynamic(string json)` | Deserializes JSON when the root type is unknown; returns `object`. |
| `ContainsRootType(string json)` | Checks whether the JSON contains a root type (`$r`) tag. |
| `GetRootType(string json)` | Returns the root `Type` from JSON serialized with `IncludeRootType`. |

### Options

Pass an `Options` instance to the `Serialize` methods:

```csharp
var options = new Serializer.Options
{
    IncludeRootType = true,
    WriteIndented = true,
    IncludeFullAssemblyInfo = false
};

string json = Serializer.Serialize(value, options);
```

| Property | Default | Description |
| --- | --- | --- |
| `IncludeRootType` | `false` | Wraps the output in `{ "$r": <type>, "$v": <value> }` so the root type is discoverable (see `GetRootType`). |
| `IncludeFullAssemblyInfo` | `false` | Uses full assembly-qualified names for type identifiers instead of short codes / full names. |
| `WriteIndented` | `false` | Writes indented JSON. |

## How it works

### Type tags

Values whose runtime type differs from the declared type are wrapped in a
small envelope:

```json
{ "$t": "<type-code>", "$v": <value> }
```

| Field | Meaning |
| --- | --- |
| `$t` | The type identifier — a short code for built-ins, otherwise the full (or assembly-qualified) type name. |
| `$v` | The value itself, encoded according to the type. |
| `$r` | Root type tag, used only with `IncludeRootType`. |
| `$k` / `$v` | Key/value entries used for dictionaries with non-string keys. |

### Serialization rules

1. If the runtime type **exactly matches** the declared (static) type, the value
   is emitted **without** a `$t` tag.
2. If the runtime type **differs** from the declared type, or the declared type
   is `object`, an interface, or an abstract class, the value is wrapped in a
   `$t` / `$v` envelope.
3. Properties of complex objects are always serialized.
4. Native JSON types (`bool`, numeric types, `string`, …) are emitted as
   plain JSON values.
5. Types not natively representable in JSON (`DateTime`, `Guid`, `decimal`,
   `TimeSpan`, `Uri`, `Version`, `char`, enums) are serialized as strings to
   preserve precision and fidelity.

### Short type codes

Built-in and well-known value types use compact codes:

| Type | Code | Type | Code |
| --- | --- | --- | --- |
| `bool` | `b` | `int` | `i` |
| `byte` | `by` | `uint` | `ui` |
| `sbyte` | `sb` | `long` | `l` |
| `char` | `c` | `ulong` | `ul` |
| `short` | `sh` | `float` | `f` |
| `ushort` | `ush` | `double` | `d` |
| `string` | `s` | `decimal` | `dec` |
| `DateTime` | `dt` | `DateTimeOffset` | `dto` |
| `TimeSpan` | `ts` | `Guid` | `g` |
| `Uri` | `uri` | `Version` | `ver` |
| `object` | `o` | `object[]` | `oa` |

Nullable variants append `?` (e.g. `"i?"` for `int?`).

Complex user types use their full type name (or assembly-qualified name when
`IncludeFullAssemblyInfo` is enabled).

### Dictionaries

- **String-keyed dictionaries** are serialized as JSON objects.
- **Non-string-keyed dictionaries** are serialized as an array of
  `{ "$k": <key>, "$v": <value> }` entries.

### Type resolution

During deserialization, type codes are resolved in order:

1. The built-in short-code table.
2. A per-process cache of previously resolved type names.
3. `Type.GetType` (handles assembly-qualified names).
4. A scan of all loaded assemblies by `FullName` or `Name`.

If a type cannot be resolved (for example, a user type from an assembly that is
not loaded), an `InvalidOperationException` is thrown. Ensure the relevant
assembly is loaded before deserializing.

## Logging

The library accepts an `ILogger` from `Microsoft.Extensions.Logging`:

```csharp
using Microsoft.Extensions.Logging;
using DynTypeSerializer.Logging;

SerializerLogging.Configure(loggerFactory.CreateLogger("DynTypeSerializer"));
```

If no logger is configured, `NullLogger.Instance` is used (no-op).

## Limitations

- **Private/static properties are ignored** — only public instance properties
  with a readable getter and no index parameters are serialized.
- **Fields are ignored** — serialization is property-based.
- **No constructor injection** — objects are created with
  `Activator.CreateInstance` and populated via public setters; types without a
  parameterless constructor (or with read-only properties) may not round-trip
  fully.
- **No circular reference detection** — self-referencing object graphs will
  cause unbounded recursion.
- **No support for `System.Object` graphs containing delegates, pointers,
  `IntPtr`, or other unrepresentable types** — these are not handled by the
  primitive encoder.
- Types must be **public** and discoverable via reflection at deserialization
  time.

## Building

```powershell
dotnet restore
dotnet build -c Release
dotnet pack -c Release
```

The package is written to `bin\Release\DynTypeSerializer.<Version>.nupkg`.
See [`build.txt`](build.txt) for the full set of build instructions, including
how to build with warnings-as-errors and clean the output.

## Testing

The repository includes an xUnit test suite in
[`tests/DynTypeSerializer.Tests`](tests/DynTypeSerializer.Tests). It covers the
whole public surface of the library.

### Running the tests

```powershell
dotnet test tests/DynTypeSerializer.Tests -c Release
```

### What is covered

| File | Coverage |
| --- | --- |
| `SerializeTests.cs` | Serialization output shape: primitives, boxed-value `$t`/`$v` tags, `DateTime`/`Guid`/`TimeSpan`/`decimal` encoding, enums, dictionaries, arrays, polymorphic type tags, `Type` properties, indentation. |
| `DeserializeTests.cs` | Reading JSON back into typed values: tagged envelopes, dynamic root deserialization, dictionaries, arrays, `IncludeRootType` handling, and error cases (unknown types, malformed JSON, null results). |
| `RoundTripTests.cs` | Round-trip fidelity — `Deserialize(Serialize(x)) ≈ x` for every supported type, complex models, polymorphism, nested graphs, and value precision. |
| `OptionsAndMetadataTests.cs` | `Serializer.Options` (`IncludeRootType`, `WriteIndented`, `IncludeFullAssemblyInfo`) and the `ContainsRootType` / `GetRootType` helpers. |
| `LoggingTests.cs` | The public `SerializerLogging.Configure(ILogger)` surface, including the no-op `NullLogger` fallback. |
| `Models/TestModels.cs` | Shared test types used across the suite. |
| `TestDoubles/RecordingLogger.cs` | An in-memory `ILogger` used to assert on emitted log output. |

### Notes on behavior the tests assert

- To preserve the runtime type of a boxed value you must serialize it with an
  `object` declared type — i.e. `Serialize<object>(value)` — which emits the
  `$t` tag. The non-generic `Serialize(value)` overload uses the actual runtime
  type as the declared type, so it does not tag the root value.
- `Deserialize<T>` throws `InvalidOperationException("Deserialization resulted
  in null.")` rather than returning `null` when the result is null.
- The internal level-specific logging helpers
  (`SerializerLogging.Debug/Info/Warning/Error`) are not part of the public
  API and are not currently exercised; the tests target the public
  `Configure(ILogger)` entry point.

## License

This project is released into the public domain under
[The Unlicense](LICENSE). You can do whatever you want with it — copy, modify,
publish, use, compile, sell, or distribute it, for any purpose, commercial or
non-commercial.
