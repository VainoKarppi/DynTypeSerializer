# Known Issues & Observations

This file catalogs issues and behavioral observations found while building out
the xUnit test suite (`tests/DynTypeSerializer.Tests`) and reviewing the
library code. The test suite currently passes (97/97); most entries below are
behavioral quirks, non-standard patterns, or latent bugs that the tests either
document or highlight — not necessarily failing tests.

Legend:
- **Bug** — incorrect or surprising behavior that likely doesn't match intent.
- **Improvement** — a design/pattern change that would align the library with
  Microsoft/.NET conventions.
- **Limitation** — documented, accepted constraint (often unavoidable).

---

## 1. `SerializerLogging` internal helpers are dead code — **Improvement**

**Where:** `Logging.cs`

The public `Configure(ILogger)` method and the internal
`Debug`/`Info`/`Warning`/`Error` methods are **never called anywhere** in the
library. None of the `Serialize`/`Deserialize`/`ResolveType` code paths emit a
single log message.

**Impact:** The logging infrastructure is unused; enabling a logger produces
only an "initialized" message and no operational diagnostics.

**Suggested fix:** Wire the logging helpers into the serialization /
deserialization / type-resolution paths, or remove the unused surface.

---

## 2. Non-standard logger injection pattern — **Improvement**
**Where:** `Logging.cs` → `SerializerLogging.Configure(ILogger)`

The library exposes a custom static facade taking a single `ILogger`, which is
an anti-pattern for .NET libraries. The recommended Microsoft pattern is to
accept an `ILoggerFactory` (or use `ILogger<T>` / the `[LoggerMessage]` source
generator) so the host's factory and category filtering are honored.

**Suggested fix:** Replace with `SetLoggerFactory(ILoggerFactory)` and use the
source-generated `[LoggerMessage]` methods (allocation-free, AOT-compatible,
which matters given `<IsAotCompatible>true</IsAotCompatible>`).

---

## 3. Read-only properties are serialized but ignored on read — **Bug/Inconsistency**

**Where:** `Serialize.cs` (`ObjectToNode` serializes every readable property)
and `Deserialize.cs` (`ReadObject` skips properties without a setter).

**Repro:** `ReadOnlyModel.Computed => $"Id-{Id}"` is written to JSON but never
restored on deserialization.

**Impact:** Asymmetric output: the serializer emits values (e.g. derived
`Computed` fields) that the deserializer silently drops, so round-trips don't
preserve all emitted data.

**Suggested fix:** Skip getter-only properties during serialization too (or
provide a configurable opt-in), so serialized output and deserialized output
are consistent.

---

## 4. `decimal` is serialized as a string — **Limitation**

**Where:** `Serialize.cs` → `PrimitiveToNode`

```csharp
if (t == typeof(decimal) || t == typeof(decimal?))
    return JsonValue.Create(obj.ToString())!; // avoid float precision loss
```

Decimals are emitted as quoted strings rather than native JSON numbers.

**Impact:** Round-trips correctly, but the JSON is not "numbers as numbers",
which can be surprising to consumers outside this library and differs from
`System.Text.Json`'s default decimal handling.

**Note:** This is a deliberate precision trade-off, so it is documented as a
limitation, not a defect.

---

## 5. Enum type resolution depends on loaded-assembly scanning — **Latent risk**

**Where:** `DynTypeSerializer.cs` → `ResolveType`

When an enum/type has no short code, resolution falls back to
`AppDomain.CurrentDomain.GetAssemblies()...`. In Native AOT (`IsAotCompatible`
is enabled) and trimmed scenarios, reflection over
`AppDomain.CurrentDomain.GetAssemblies()` is restricted and this scan can fail
to find types.

**Impact:** Boxed enums and user types may fail to deserialize under AOT/trimmed
deployment.

**Suggested fix:** Provide a registration/`JsonSerializerContext`-style type
table, or document the AOT constraint; several related reflection warnings are
already suppressed in the `.csproj`.

---

## 6. Library project greedily globs subfolders — **Improvement (build)**

**Where:** `DynTypeSerializer.csproj`

Because the library lives at the repository root, its default `**/*.cs` glob
includes any subfolder (e.g. `tests/**`). Adding the test project broke the
library build until an exclusion was added:

```xml
<Compile Remove="tests\**\*.cs" />
```

**Impact:** Fragile — any future sibling folder (samples, benchmarks) also gets
compiled into the library unless excluded.

**Suggested fix:** Restructure to `src/DynTypeSerializer` + `tests/...` with a
root solution (or keep adding exclusions).

---

## 7. `ContainsRootType` / `GetRootType` re-parse JSON — **Performance**

**Where:** `DynTypeSerializer.cs`

Both methods parse the entire JSON document independently. For large payloads
this duplicates parsing work versus inspecting an already-parsed document.

**Impact:** Minor performance overhead when these helpers are used in
hot paths.

---

## Summary

| # | Area | Kind | Severity |
|---|------|------|----------|
| 1 | Logging helpers are dead code | Improvement | Low |
| 2 | Non-standard logger injection | Improvement | Low |
| 3 | Read-only props asymmetric | Bug/Inconsistency | Low |
| 4 | `decimal` as string | Limitation | Info |
| 5 | Enum resolution vs AOT | Latent risk | Medium |
| 6 | Root project globs subfolders | Improvement (build) | Low |
| 7 | Root helpers re-parse JSON | Performance | Low |
