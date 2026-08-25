# Known Issues & Observations

This file catalogs issues and behavioral observations found while building out
the xUnit test suite (`tests/DynTypeSerializer.Tests`) and reviewing the
library code. The test suite currently passes (90/90); most entries below are
behavioral quirks, non-standard patterns, or latent bugs that the tests either
document or highlight — not necessarily failing tests.

Legend:
- **Bug** — incorrect or surprising behavior that likely doesn't match intent.
- **Improvement** — a design/pattern change that would align the library with
  Microsoft/.NET conventions.
- **Limitation** — documented, accepted constraint (often unavoidable).
- **✅ Fixed** — resolved; the entry is kept for historical context.

---

## 1. Non-generic `Serialize(object)` never tags the root value — **Improvement / Bug**

**Where:** `Serialize.cs` → `Serialize(object? obj, ...)`

The non-generic overload passes the object's **actual runtime type** as the
declared type:

```csharp
JsonNode? node = BuildNode(obj, obj?.GetType() ?? typeof(object), options);
```

Because the declared type and runtime type are identical, `NeedsTypeTag`
returns `false`, so **no `$t` tag is emitted at the root** — even for a boxed
value where you'd expect type preservation.

**Repro:**

```csharp
object value = 42;
Serializer.Serialize(value);        // "42"          (no $t tag)
Serializer.Serialize<object>(value); // {"$t":"i","$v":42}  (tagged)
```

**Impact:** Calling `Serialize(value)` followed by `Deserialize<object>(...)`
does **not** round-trip the type — a boxed `int` comes back as a `long`, and a
`Dog` instance comes back as a `Dictionary<string, object>`.

**Suggested fix:** Treat the non-generic overload as `object`-declared by
passing `typeof(object)` as the declared type, so the root is always tagged for
type preservation.

---

## 2. `Deserialize<T>` throws instead of returning `null` — **Bug** ✅ Fixed

**Where:** `Deserialize.cs` → `Deserialize<T>(string json)`

**Fixed in commit/change:** The `if (result == null) throw ...` guard was
removed; `Deserialize<T>` now returns `default(T)` when the value is null.

```csharp
object? result = ReadNode(root, typeof(T));
return result is null ? default : (T)result;
```

Deserializing a JSON `null` (or a value that resolves to null) now returns
`null`/`default(T)` rather than throwing.

**Repro:**

```csharp
Serializer.Deserialize<string?>("null"); // now returns null
```

**Impact:** Consistent with conventional serializer behavior; callers no
longer need try/catch to handle a legitimate `null` payload.

---

## 3. Numbers in the `object` fallback become strings — **Bug** ✅ Fixed

**Where:** `Deserialize.cs` → `ReadPrimitive`

**Fixed in commit/change:** Added a `ReadJsonValueAsObject` helper that maps a
raw `JsonElement` to its natural .NET type when the target is `object` (number
→ `long`/`double`, `true`/`false` → `bool`, etc.), and `ReadPrimitive` now
routes `object`-typed reads through it instead of `Convert.ChangeType`.

**Repro:**

```csharp
var r = Serializer.DeserializeDynamic("{\"a\":1,\"b\":1.5,\"c\":true}");
// r["a"] is long 1, r["b"] is double 1.5, r["c"] is bool true
```

**Impact:** Dynamic deserialization of a plain JSON object now preserves
numeric and boolean value kinds, restoring round-trip type fidelity for
untagged data.

---

## 4. `SerializerLogging` internal helpers are dead code — **Improvement**

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

## 5. Non-standard logger injection pattern — **Improvement**

**Where:** `Logging.cs` → `SerializerLogging.Configure(ILogger)`

The library exposes a custom static facade taking a single `ILogger`, which is
an anti-pattern for .NET libraries. The recommended Microsoft pattern is to
accept an `ILoggerFactory` (or use `ILogger<T>` / the `[LoggerMessage]` source
generator) so the host's factory and category filtering are honored.

**Suggested fix:** Replace with `SetLoggerFactory(ILoggerFactory)` and use the
source-generated `[LoggerMessage]` methods (allocation-free, AOT-compatible,
which matters given `<IsAotCompatible>true</IsAotCompatible>`).

---

## 6. No circular-reference detection — **Limitation**

**Where:** `Serialize.cs` → `BuildNode` / `ObjectToNode`

Self-referencing object graphs recurse without bound and will cause a
`StackOverflowException`.

**Suggested fix:** Track a visited-set or depth limit and throw a descriptive
exception (or reference a documented limitation more clearly).

---

## 7. Read-only properties are serialized but ignored on read — **Bug/Inconsistency**

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

## 8. `decimal` is serialized as a string — **Limitation**

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

## 9. Enum type resolution depends on loaded-assembly scanning — **Latent risk**

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

## 10. Library project greedily globs subfolders — **Improvement (build)**

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

## 11. `GetRootType` swallows all exceptions — **Minor**

**Where:** `DynTypeSerializer.cs` → `GetRootType`

```csharp
} catch {
    return null;
}
```

Any parse/resolution failure returns `null`, making it hard to distinguish "no
root type" from "malformed JSON" or "unresolvable type".

**Suggested fix:** Narrow the catch or rethrow non-"no tag" failures.

---

## 12. `ContainsRootType` / `GetRootType` re-parse JSON — **Performance**

**Where:** `DynTypeSerializer.cs`

Both methods parse the entire JSON document independently. For large payloads
this duplicates parsing work versus inspecting an already-parsed document.

**Impact:** Minor performance overhead when these helpers are used in
hot paths.

---

## Summary

| # | Area | Kind | Severity |
|---|------|------|----------|
| 1 | `Serialize(object)` no root tag | Bug/Improvement | High |
| 2 | `Deserialize<T>` throws on null | Bug ✅ Fixed | Medium |
| 3 | Numbers become strings in `object` fallback | Bug ✅ Fixed | Medium |
| 4 | Logging helpers are dead code | Improvement | Low |
| 5 | Non-standard logger injection | Improvement | Low |
| 6 | No circular-ref detection | Limitation | Low |
| 7 | Read-only props asymmetric | Bug/Inconsistency | Low |
| 8 | `decimal` as string | Limitation | Info |
| 9 | Enum resolution vs AOT | Latent risk | Medium |
| 10 | Root project globs subfolders | Improvement (build) | Low |
| 11 | `GetRootType` swallows errors | Minor | Low |
| 12 | Root helpers re-parse JSON | Performance | Low |
