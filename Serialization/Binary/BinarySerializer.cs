using System.Collections;
using System.Reflection;
using DynTypeSerializer.Serialization;

namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Writes objects to the compact binary wire format.
/// </summary>
/// <remarks>
/// Mirrors the JSON serializer's traversal logic but writes directly to a
/// <see cref="BinaryWriter"/> — never through JSON.
///
/// The wire format writes a single-byte type token before each value, then the
/// value payload when applicable. Inside homogeneous known-type collections a
/// per-element token may be omitted (known-type optimization) — see
/// <see cref="WriteEnumerable"/>.
/// </remarks>
internal static class BinarySerializer
{
    /// <summary>Writes an object as binary bytes.</summary>
    public static byte[] Serialize(object? obj, Serializer.Options options)
    {
        var writer = new BinaryWriter();
        var registry = new BinaryTypeRegistry();

        BinaryFormat.WriteHeader(writer);
        WriteValue(writer, obj, obj?.GetType() ?? typeof(object), options, registry, null, 0);

        return writer.ToArray();
    }

    private static void WriteValue(BinaryWriter writer, object? obj, Type declaredType,
        Serializer.Options options, BinaryTypeRegistry registry, HashSet<object>? visiting, int depth)
    {
        if (obj is null)
        {
            writer.WriteTypeCode(BinaryTypeCode.Null);
            return;
        }

        if (depth > options.MaxSerializationDepth)
            throw new InvalidOperationException(
                "DynTypeSerializer: maximum serialization depth exceeded. " +
                "The object graph is likely too deep or contains a cycle.");

        if (!obj.GetType().IsValueType)
        {
            visiting ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!visiting.Add(obj))
                throw new InvalidOperationException(
                    $"DynTypeSerializer: circular reference detected while serializing " +
                    $"an object of type '{obj.GetType().FullName}'. " +
                    "The object graph contains a cycle that cannot be represented.");

            try { WriteValueCore(writer, obj, declaredType, options, registry, visiting, depth); }
            finally { visiting.Remove(obj); }
        }
        else
        {
            WriteValueCore(writer, obj, declaredType, options, registry, visiting, depth);
        }
    }

    private static void WriteValueCore(BinaryWriter writer, object obj, Type declaredType,
        Serializer.Options options, BinaryTypeRegistry registry, HashSet<object>? visiting, int depth)
    {
        Type actualType = obj.GetType();
        int nextDepth = depth + 1;

        if (obj is IDictionary dict)
        {
            writer.WriteTypeCode(BinaryTypeCode.Dictionary);
            WriteDictionary(writer, dict, actualType, options, registry, visiting, nextDepth);
            return;
        }

        if (obj is IEnumerable enumerable && obj is not string)
        {
            if (actualType.IsArray)
                writer.WriteTypeCode(BinaryTypeCode.Array);
            else
                writer.WriteTypeCode(BinaryTypeCode.List);
            WriteEnumerable(writer, enumerable, actualType, options, registry, visiting, nextDepth);
            return;
        }

        if (SerializerCore.IsPrimitiveLike(actualType))
        {
            WritePrimitiveValue(writer, obj, actualType, registry);
            return;
        }

        // Complex object.
        writer.WriteTypeCode(BinaryTypeCode.Object);
        WriteObject(writer, obj, actualType, options, registry, visiting, nextDepth);
    }

    private static void WritePrimitiveValue(BinaryWriter writer, object obj, Type t,
        BinaryTypeRegistry registry)
    {
        switch (obj)
        {
            case bool b:
                writer.WriteTypeCode(b ? BinaryTypeCode.True : BinaryTypeCode.False);
                return;
            case char c:
                writer.WriteTypeCode(BinaryTypeCode.Char);
                writer.WriteVarUInt32((uint)c);
                return;
            case sbyte v:
                writer.WriteTypeCode(BinaryTypeCode.Int); writer.WriteVarInt64(v); return;
            case byte v:
                writer.WriteTypeCode(BinaryTypeCode.UInt); writer.WriteVarUInt64(v); return;
            case short v:
                writer.WriteTypeCode(BinaryTypeCode.Int); writer.WriteVarInt64(v); return;
            case ushort v:
                writer.WriteTypeCode(BinaryTypeCode.UInt); writer.WriteVarUInt64(v); return;
            case int v:
                writer.WriteTypeCode(BinaryTypeCode.Int); writer.WriteVarInt64(v); return;
            case uint v:
                writer.WriteTypeCode(BinaryTypeCode.UInt); writer.WriteVarUInt64(v); return;
            case long v:
                writer.WriteTypeCode(BinaryTypeCode.Int); writer.WriteVarInt64(v); return;
            case ulong v:
                writer.WriteTypeCode(BinaryTypeCode.UInt); writer.WriteVarUInt64(v); return;
            case float f:
                writer.WriteTypeCode(BinaryTypeCode.Float);
                writer.WriteInt32Fixed(BitConverter.SingleToInt32Bits(f));
                return;
            case double d:
                writer.WriteTypeCode(BinaryTypeCode.Double);
                writer.WriteInt64Fixed(BitConverter.DoubleToInt64Bits(d));
                return;
            case decimal m:
                writer.WriteTypeCode(BinaryTypeCode.Decimal);
                foreach (int part in decimal.GetBits(m))
                    writer.WriteInt32Fixed(part);
                return;
            case string s:
                writer.WriteTypeCode(BinaryTypeCode.String);
                writer.WriteString(s);
                return;
            case TimeSpan ts:
                writer.WriteTypeCode(BinaryTypeCode.String); writer.WriteString(ts.ToString("c")); return;
            case DateTime dt:
                writer.WriteTypeCode(BinaryTypeCode.String); writer.WriteString(dt.ToString("O")); return;
            case DateTimeOffset dto:
                writer.WriteTypeCode(BinaryTypeCode.String); writer.WriteString(dto.ToString("O")); return;
            case Guid g:
                writer.WriteTypeCode(BinaryTypeCode.String); writer.WriteString(g.ToString()); return;
            case Uri uri:
                writer.WriteTypeCode(BinaryTypeCode.String); writer.WriteString(uri.ToString()); return;
            case Version version:
                writer.WriteTypeCode(BinaryTypeCode.String); writer.WriteString(version.ToString()); return;
            default:
                if (t.IsEnum)
                {
                    writer.WriteTypeCode(BinaryTypeCode.Object);
                    // Register the enum type so the reader can parse it back.
                    uint id = registry.GetTypeId(t);
                    writer.WriteVarUInt32(id);
                    writer.WriteVarInt64(Convert.ToInt64(obj));
                }
                else
                {
                    throw new InvalidOperationException(
                        $"DynTypeSerializer: cannot serialize value of type '{t.FullName}' in binary format.");
                }
                return;
        }
    }

    private static void WriteDictionary(BinaryWriter writer, IDictionary dict, Type actualType,
        Serializer.Options options, BinaryTypeRegistry registry, HashSet<object>? visiting, int depth)
    {
        Type keyType = actualType.IsGenericType ? actualType.GetGenericArguments()[0] : typeof(string);
        Type valueType = actualType.IsGenericType ? actualType.GetGenericArguments()[1] : typeof(object);

        writer.WriteVarUInt32((uint)dict.Count);
        foreach (DictionaryEntry kv in dict)
        {
            WriteValue(writer, kv.Key, keyType, options, registry, visiting, depth);
            WriteValue(writer, kv.Value, valueType, options, registry, visiting, depth);
        }
    }

    private static void WriteEnumerable(BinaryWriter writer, IEnumerable enumerable, Type actualType,
        Serializer.Options options, BinaryTypeRegistry registry, HashSet<object>? visiting, int depth)
    {
        Type elemType = actualType.IsArray
            ? actualType.GetElementType()!
            : actualType.IsGenericType
                ? actualType.GetGenericArguments()[0]
                : typeof(object);

        var items = enumerable.Cast<object?>().ToList();
        writer.WriteVarUInt32((uint)items.Count);

        // Known-type optimization: when the element type is a concrete,
        // non-object primitive, omit per-element tokens and read them with the
        // declared element type. Otherwise recurse with per-value tokens.
        bool knownHomogeneous = elemType != typeof(object)
            && !elemType.IsInterface
            && !elemType.IsAbstract
            && SerializerCore.IsPrimitiveLike(elemType);

        if (knownHomogeneous)
        {
            foreach (var item in items)
            {
                if (item is null)
                {
                    writer.WriteTypeCode(BinaryTypeCode.Null);
                    continue;
                }
                // No token — write the raw value for the known element type.
                WritePrimitiveValueKnownType(writer, item, elemType, registry);
            }
        }
        else
        {
            foreach (var item in items)
                WriteValue(writer, item, elemType, options, registry, visiting, depth);
        }
    }

    /// <summary>
    /// Writes a primitive value WITHOUT a leading token, for use inside a
    /// homogeneous known-type collection.
    /// </summary>
    private static void WritePrimitiveValueKnownType(BinaryWriter writer, object obj, Type t,
        BinaryTypeRegistry registry)
    {
        switch (obj)
        {
            case sbyte v: writer.WriteVarInt64(v); return;
            case byte v: writer.WriteVarUInt64(v); return;
            case short v: writer.WriteVarInt64(v); return;
            case ushort v: writer.WriteVarUInt64(v); return;
            case int v: writer.WriteVarInt64(v); return;
            case uint v: writer.WriteVarUInt64(v); return;
            case long v: writer.WriteVarInt64(v); return;
            case ulong v: writer.WriteVarUInt64(v); return;
            case float f: writer.WriteInt32Fixed(BitConverter.SingleToInt32Bits(f)); return;
            case double d: writer.WriteInt64Fixed(BitConverter.DoubleToInt64Bits(d)); return;
            case decimal m:
                foreach (int part in decimal.GetBits(m))
                    writer.WriteInt32Fixed(part);
                return;
            case string s: writer.WriteString(s); return;
            case DateTime dt: writer.WriteString(dt.ToString("O")); return;
            case TimeSpan ts: writer.WriteString(ts.ToString("c")); return;
            case Guid g: writer.WriteString(g.ToString()); return;
            case bool b:
                // bool is not a homogeneous primitive leaf here (it has its own
                // token); fall back to tokenized write.
                writer.WriteTypeCode(b ? BinaryTypeCode.True : BinaryTypeCode.False);
                return;
            default:
                writer.WriteTypeCode(BinaryTypeCode.Object);
                uint id = registry.GetTypeId(t);
                writer.WriteVarUInt32(id);
                writer.WriteVarInt64(Convert.ToInt64(obj));
                return;
        }
    }

    private static void WriteObject(BinaryWriter writer, object obj, Type actualType,
        Serializer.Options options, BinaryTypeRegistry registry, HashSet<object>? visiting, int depth)
    {
        foreach (var prop in SerializerCore.GetProperties(actualType))
        {
            object? val = prop.GetValue(obj);

            if (val is Type typeVal)
            {
                writer.WriteTypeCode(BinaryTypeCode.Object);
                writer.WriteVarUInt32(registry.GetTypeId(typeof(Type)));
                writer.WriteString(typeVal.FullName ?? typeVal.Name);
            }
            else
            {
                WriteValue(writer, val, prop.PropertyType, options, registry, visiting, depth);
            }
        }
    }
}


