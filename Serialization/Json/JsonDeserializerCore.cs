using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace DynTypeSerializer.Serialization.Json;

/// <summary>
/// Internal JSON deserializer. Reads the type-preserving JSON format back into
/// objects. Mirrors the traversal rules shared with the binary deserializer.
/// </summary>
internal static class JsonDeserializerCore
{
    public static T? Deserialize<T>(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("$r", out var rProp) && root.TryGetProperty("$v", out var vProp))
        {
            return (T?)ReadNode(vProp, typeof(T));
        }

        object? result = ReadNode(root, typeof(T));
        return result is null ? default : (T)result;
    }

    public static object? DeserializeDynamic(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ReadNode(doc.RootElement, typeof(object));
    }

    private static object? ReadNode(JsonElement el, Type declaredType)
    {
        if (el.ValueKind == JsonValueKind.Null) return null;

        Type targetType = declaredType;
        JsonElement valueEl = el;

        if (el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty("$t", out var tProp)
            && el.TryGetProperty("$v", out var vProp))
        {
            string code = tProp.GetString()
                ?? throw new InvalidOperationException("$t code was null.");
            targetType = SerializerCore.ResolveType(code);
            valueEl    = vProp;
        }

        return ReadValue(valueEl, targetType);
    }

    private static object? ReadValue(JsonElement el, Type targetType)
    {
        if (el.ValueKind == JsonValueKind.Null) return null;

        Type innerType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (SerializerCore.IsPrimitiveLike(innerType))
            return ReadPrimitive(el, innerType);

        if (typeof(IDictionary).IsAssignableFrom(innerType))
            return ReadDict(el, innerType);

        if (el.ValueKind == JsonValueKind.Object)
            return ReadObject(el, innerType);

        if (el.ValueKind == JsonValueKind.Array)
            return ReadList(el, innerType);

        return ReadPrimitive(el, innerType);
    }

    private static object ReadPrimitive(JsonElement el, Type t)
    {
        if (t == typeof(object))
            return ReadJsonValueAsObject(el)!;

        string raw = el.ValueKind == JsonValueKind.String
            ? el.GetString()!
            : el.GetRawText();

        if (t == typeof(string))         return el.GetString()!;
        if (t == typeof(bool))           return el.GetBoolean();
        if (t == typeof(byte))           return el.GetByte();
        if (t == typeof(sbyte))          return el.GetSByte();
        if (t == typeof(short))          return el.GetInt16();
        if (t == typeof(ushort))         return el.GetUInt16();
        if (t == typeof(int))            return el.GetInt32();
        if (t == typeof(uint))           return el.GetUInt32();
        if (t == typeof(long))           return el.GetInt64();
        if (t == typeof(ulong))          return el.GetUInt64();
        if (t == typeof(float))          return el.GetSingle();
        if (t == typeof(double))         return el.GetDouble();
        if (t == typeof(decimal))        return decimal.Parse(raw);
        if (t == typeof(char))           return raw.Length > 0 ? raw[0] : '\0';
        if (t == typeof(Guid))           return Guid.Parse(raw);
        if (t == typeof(DateTime))       return DateTime.Parse(raw);
        if (t == typeof(DateTimeOffset)) return DateTimeOffset.Parse(raw);
        if (t == typeof(TimeSpan))       return TimeSpan.Parse(raw);
        if (t == typeof(Uri))            return new Uri(raw);
        if (t == typeof(Version))        return Version.Parse(raw);
        if (t.IsEnum)                    return Enum.Parse(t, raw);

        return Convert.ChangeType(raw, t);
    }

    private static object? ReadJsonValueAsObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String   => el.GetString(),
            JsonValueKind.Number   => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
            JsonValueKind.True     => true,
            JsonValueKind.False    => false,
            JsonValueKind.Null     => null,
            JsonValueKind.Object   => ReadObject(el, typeof(object)),
            JsonValueKind.Array    => ReadList(el, typeof(List<object?>)),
            _                      => el.GetRawText()
        };
    }

    private static IDictionary ReadDict(JsonElement el, Type dictType)
    {
        Type keyType   = dictType.IsGenericType ? dictType.GetGenericArguments()[0] : typeof(string);
        Type valueType = dictType.IsGenericType ? dictType.GetGenericArguments()[1] : typeof(object);

        Type concrete = dictType.IsInterface || dictType.IsAbstract
            ? typeof(Dictionary<,>).MakeGenericType(keyType, valueType)
            : dictType;

        var dict = (IDictionary)Activator.CreateInstance(concrete)!;

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                object key = keyType == typeof(string)
                    ? prop.Name
                    : Convert.ChangeType(prop.Name, keyType);
                dict[key] = ReadNode(prop.Value, valueType);
            }
            return dict;
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new InvalidOperationException("Dictionary array entries must be objects with '$k' and '$v'.");

                if (!item.TryGetProperty("$k", out var keyEl) || !item.TryGetProperty("$v", out var valueEl))
                    throw new InvalidOperationException("Dictionary array entries must contain '$k' and '$v'.");

                object? key = ReadNode(keyEl, keyType);
                dict[key!] = ReadNode(valueEl, valueType);
            }
            return dict;
        }

        throw new InvalidOperationException($"Cannot deserialize dictionary from JSON token {el.ValueKind}.");
    }

    private static object ReadObject(JsonElement el, Type targetType)
    {
        if (targetType == typeof(object) || targetType.IsInterface)
        {
            var fallback = new Dictionary<string, object?>();
            foreach (var prop in el.EnumerateObject())
                fallback[prop.Name] = ReadNode(prop.Value, typeof(object));
            return fallback;
        }

        object instance = Activator.CreateInstance(targetType)
            ?? throw new InvalidOperationException($"Cannot create instance of {targetType}");

        foreach (var prop in SerializerCore.GetProperties(targetType))
        {
            if (!prop.CanWrite)
            {
                Serializer.LogWarning(TypeCodeWarning.SkippedReadOnlyProperty,
                    (prop.Name, targetType.FullName ?? targetType.Name));
                continue;
            }
            if (!el.TryGetProperty(prop.Name, out var val)) continue;

            object? propValue;

            if (prop.PropertyType == typeof(Type))
            {
                string? typeName = val.GetString();
                propValue = string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            }
            else
            {
                propValue = ReadNode(val, prop.PropertyType);
            }

            prop.SetValue(instance, propValue);
        }
        return instance;
    }

    private static object ReadList(JsonElement el, Type targetType)
    {
        Type elemType = targetType.IsArray
            ? targetType.GetElementType()!
            : targetType.IsGenericType
                ? targetType.GetGenericArguments()[0]
                : typeof(object);

        Type listType = typeof(List<>).MakeGenericType(elemType);
        var  list     = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in el.EnumerateArray())
            list.Add(ReadNode(item, elemType));

        if (targetType.IsArray)
        {
            var arr = Array.CreateInstance(elemType, list.Count);
            list.CopyTo(arr, 0);
            return arr;
        }

        if (targetType.IsAssignableFrom(listType)) return list;

        return Activator.CreateInstance(targetType, list)
               ?? throw new InvalidOperationException($"Cannot construct {targetType} from list.");
    }
}
