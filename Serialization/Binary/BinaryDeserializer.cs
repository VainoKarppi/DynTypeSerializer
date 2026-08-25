using System.Collections;
using System.Reflection;
using DynTypeSerializer.Serialization;

namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Reads objects from the compact binary wire format produced by
/// <see cref="BinarySerializer"/>.
/// </summary>
/// <remarks>
/// Mirrors the JSON deserializer's traversal logic, reading directly from a
/// <see cref="ReadOnlySpan{T}"/>. All counts/lengths are validated against the
/// remaining buffer before any allocation.
/// </remarks>
internal static class BinaryDeserializer
{
    public static T? Deserialize<T>(ReadOnlySpan<byte> data, Serializer.Options options)
    {
        var reader = new BinaryReader(data);
        BinaryFormat.ReadAndValidateHeader(ref reader);
        var registry = new BinaryTypeRegistry();

        object? result = ReadValue(ref reader, typeof(T), options, registry, 0);
        return result is null ? default : (T)result;
    }

    public static object? DeserializeDynamic(byte[] data, Serializer.Options options)
    {
        var reader = new BinaryReader(data);
        BinaryFormat.ReadAndValidateHeader(ref reader);
        var registry = new BinaryTypeRegistry();
        return ReadValue(ref reader, typeof(object), options, registry, 0);
    }

    private static object? ReadValue(ref BinaryReader reader, Type declaredType,
        Serializer.Options options, BinaryTypeRegistry registry, int depth)
    {
        if (depth > options.MaxSerializationDepth)
            throw new InvalidDataException("Binary payload exceeds the maximum nesting depth.");

        BinaryTypeCode code = reader.ReadTypeCode();
        return ReadValueForCode(ref reader, code, declaredType, options, registry, depth);
    }

    private static object? ReadValueForCode(ref BinaryReader reader, BinaryTypeCode code, Type declaredType,
        Serializer.Options options, BinaryTypeRegistry registry, int depth)
    {
        switch (code)
        {
            case BinaryTypeCode.Null:
                return null;
            case BinaryTypeCode.True:
                return true;
            case BinaryTypeCode.False:
                return false;
            case BinaryTypeCode.Int:
            case BinaryTypeCode.UInt:
                return ReadInteger(ref reader, code, ResolveLeafType(declaredType));
            case BinaryTypeCode.Char:
                return (char)reader.ReadVarUInt32();
            case BinaryTypeCode.Float:
                return BitConverter.Int32BitsToSingle(reader.ReadInt32Fixed());
            case BinaryTypeCode.Double:
                return BitConverter.Int64BitsToDouble(reader.ReadInt64Fixed());
            case BinaryTypeCode.Decimal:
                return ReadDecimal(ref reader);
            case BinaryTypeCode.String:
                return ReadStringValue(ref reader, ResolveLeafType(declaredType));
            case BinaryTypeCode.Array:
                return ReadList(ref reader, declaredType, options, registry, depth, isArray: true);
            case BinaryTypeCode.List:
                return ReadList(ref reader, declaredType, options, registry, depth, isArray: false);
            case BinaryTypeCode.Dictionary:
                return ReadDictionary(ref reader, declaredType, options, registry, depth);
            case BinaryTypeCode.Object:
                return ReadObject(ref reader, declaredType, options, registry, depth);
            default:
                throw new InvalidDataException($"Unknown binary type code 0x{(byte)code:X2}.");
        }
    }

    private static Type ResolveLeafType(Type declaredType)
        => Nullable.GetUnderlyingType(declaredType) ?? declaredType;

    private static object ReadInteger(ref BinaryReader reader, BinaryTypeCode code, Type targetType)
    {
        if (code == BinaryTypeCode.Int)
        {
            long value = reader.ReadVarInt64();
            return ConvertToIntegral(value, targetType);
        }
        else
        {
            ulong value = reader.ReadVarUInt64();
            return ConvertToUnsignedIntegral(value, targetType);
        }
    }

    private static object ConvertToIntegral(long value, Type t)
    {
        if (t == typeof(object)) return value;
        if (t == typeof(int)) return (int)value;
        if (t == typeof(long)) return value;
        if (t == typeof(short)) return (short)value;
        if (t == typeof(sbyte)) return (sbyte)value;
        if (t.IsEnum) return Enum.ToObject(t, value);
        return Convert.ChangeType(value, t);
    }

    private static object ConvertToUnsignedIntegral(ulong value, Type t)
    {
        if (t == typeof(object)) return value;
        if (t == typeof(uint)) return (uint)value;
        if (t == typeof(ulong)) return value;
        if (t == typeof(ushort)) return (ushort)value;
        if (t == typeof(byte)) return (byte)value;
        return Convert.ChangeType(value, t);
    }

    private static decimal ReadDecimal(ref BinaryReader reader)
    {
        int lo = reader.ReadInt32Fixed();
        int mid = reader.ReadInt32Fixed();
        int hi = reader.ReadInt32Fixed();
        int flags = reader.ReadInt32Fixed();
        return new decimal(new[] { lo, mid, hi, flags });
    }

    private static object ReadStringValue(ref BinaryReader reader, Type t)
    {
        string raw = reader.ReadString();
        if (t == typeof(object)) return raw;
        if (t == typeof(string)) return raw;
        if (t == typeof(Guid)) return Guid.Parse(raw);
        if (t == typeof(DateTime)) return DateTime.Parse(raw);
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.Parse(raw);
        if (t == typeof(TimeSpan)) return TimeSpan.Parse(raw);
        if (t == typeof(Uri)) return new Uri(raw);
        if (t == typeof(Version)) return Version.Parse(raw);
        return raw;
    }

    private static object ReadDictionary(ref BinaryReader reader, Type dictType,
        Serializer.Options options, BinaryTypeRegistry registry, int depth)
    {
        Type keyType = dictType.IsGenericType ? dictType.GetGenericArguments()[0] : typeof(string);
        Type valueType = dictType.IsGenericType ? dictType.GetGenericArguments()[1] : typeof(object);

        uint count = reader.ReadVarUInt32();
        ValidateCount(count, reader.Remaining);

        Type concrete = dictType.IsInterface || dictType.IsAbstract
            ? typeof(Dictionary<,>).MakeGenericType(keyType, valueType)
            : dictType;
        var dict = (IDictionary)Activator.CreateInstance(concrete)!;

        for (int i = 0; i < count; i++)
        {
            object? key = ReadValue(ref reader, keyType, options, registry, depth + 1);
            object? value = ReadValue(ref reader, valueType, options, registry, depth + 1);
            dict[key!] = value;
        }
        return dict;
    }

    private static object ReadList(ref BinaryReader reader, Type targetType,
        Serializer.Options options, BinaryTypeRegistry registry, int depth, bool isArray)
    {
        uint count = reader.ReadVarUInt32();
        ValidateCount(count, reader.Remaining);

        Type elemType = targetType.IsArray
            ? targetType.GetElementType()!
            : targetType.IsGenericType
                ? targetType.GetGenericArguments()[0]
                : typeof(object);

        bool knownHomogeneous = elemType != typeof(object)
            && !elemType.IsInterface
            && !elemType.IsAbstract
            && SerializerCore.IsPrimitiveLike(elemType);

        object?[] items = new object?[count];
        for (int i = 0; i < count; i++)
        {
            if (knownHomogeneous)
            {
                items[i] = ReadHomogeneousElement(ref reader, elemType, registry);
            }
            else
            {
                items[i] = ReadValue(ref reader, elemType, options, registry, depth + 1);
            }
        }

        if (isArray || targetType.IsArray)
        {
            var arr = Array.CreateInstance(elemType, (int)count);
            for (int i = 0; i < count; i++)
                arr.SetValue(items[i], i);
            return arr;
        }

        Type listType = typeof(List<>).MakeGenericType(elemType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
            list.Add(item);
        return list;
    }

    /// <summary>
    /// Reads a single element from a homogeneous known-type collection, where
    /// the serializer omitted the per-element token. A leading Null token is
    /// still honored.
    /// </summary>
    private static object? ReadHomogeneousElement(ref BinaryReader reader, Type elemType, BinaryTypeRegistry registry)
    {
        if (reader.Remaining >= 1 && reader.PeekTypeCode() == BinaryTypeCode.Null)
        {
            reader.ReadTypeCode();
            return null;
        }

        if (elemType == typeof(string)) return reader.ReadString();
        if (elemType == typeof(int)) return (int)reader.ReadVarInt64();
        if (elemType == typeof(long)) return reader.ReadVarInt64();
        if (elemType == typeof(short)) return (short)reader.ReadVarInt64();
        if (elemType == typeof(sbyte)) return (sbyte)reader.ReadVarInt64();
        if (elemType == typeof(uint)) return (uint)reader.ReadVarUInt64();
        if (elemType == typeof(ulong)) return reader.ReadVarUInt64();
        if (elemType == typeof(ushort)) return (ushort)reader.ReadVarUInt64();
        if (elemType == typeof(byte)) return (byte)reader.ReadVarUInt64();
        if (elemType == typeof(float)) return BitConverter.Int32BitsToSingle(reader.ReadInt32Fixed());
        if (elemType == typeof(double)) return BitConverter.Int64BitsToDouble(reader.ReadInt64Fixed());
        if (elemType == typeof(decimal)) return ReadDecimal(ref reader);
        if (elemType == typeof(DateTime)) return DateTime.Parse(reader.ReadString());
        if (elemType == typeof(TimeSpan)) return TimeSpan.Parse(reader.ReadString());
        if (elemType == typeof(Guid)) return Guid.Parse(reader.ReadString());

        throw new InvalidDataException(
            $"Unsupported homogeneous element type '{elemType.FullName}' in binary collection.");
    }

    private static object ReadObject(ref BinaryReader reader, Type declaredType,
        Serializer.Options options, BinaryTypeRegistry registry, int depth)
    {
        Type targetType = ResolveLeafType(declaredType);

        // Enums are encoded as Object token + type id + varint value.
        if (targetType.IsEnum || (declaredType == typeof(object) && IsEnumEncoded(ref reader)))
        {
            uint typeId = reader.ReadVarUInt32();
            long value = reader.ReadVarInt64();

            Type enumType = targetType.IsEnum
                ? targetType
                : registry.GetType(typeId) ?? throw new InvalidDataException($"Unknown type id {typeId}.");
            return Enum.ToObject(enumType, value);
        }

        object instance = Activator.CreateInstance(targetType)
            ?? throw new InvalidOperationException($"Cannot create instance of {targetType}");

        foreach (var prop in SerializerCore.GetProperties(targetType))
        {
            if (!prop.CanWrite) continue;

            object? propValue;
            if (prop.PropertyType == typeof(Type))
            {
                string typeName = reader.ReadString();
                propValue = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            }
            else
            {
                propValue = ReadValue(ref reader, prop.PropertyType, options, registry, depth + 1);
            }

            if (propValue != null || !prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                prop.SetValue(instance, propValue);
        }
        return instance;
    }

    /// <summary>
    /// When the declared type is object and we've read an Object token, it may
    /// be an enum (written as Object + type id + value) or a class instance.
    /// A class instance has a first readable property token afterwards; enums
    /// don't. We cannot perfectly disambiguate without a length marker, so for
    /// the object-typed case we conservatively treat it as an enum when the
    /// declared type is object AND the target isn't a known enum — but that is
    /// ambiguous. To keep it simple and correct for round-trips, boxed enums
    /// are NOT the focus here; typed enums are handled above.
    /// </summary>
    private static bool IsEnumEncoded(ref BinaryReader reader) => false;

    /// <summary>
    /// Guards against absurd collection/string counts in the payload that could
    /// lead to huge allocations. Always validate before allocating.
    /// </summary>
    private static void ValidateCount(uint count, int remainingBytes)
    {
        const uint MaxCollectionCount = 100_000_000;
        if (count > MaxCollectionCount || count > (uint)remainingBytes * 8 + 1)
            throw new InvalidDataException($"Payload count {count} exceeds the safety limit.");
    }
}