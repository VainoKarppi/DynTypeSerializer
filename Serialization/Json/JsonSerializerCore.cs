using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynTypeSerializer.Serialization.Json;

/// <summary>
/// Internal JSON serializer. Writes objects to the type-preserving JSON format.
/// </summary>
/// <remarks>
/// Mirrors the traversal/type-tagging rules shared with the binary serializer.
/// The public entry point is <see cref="global::DynTypeSerializer.Serializer"/>.
/// </remarks>
internal static class JsonSerializerCore
{
    public static string Serialize(object? obj, Serializer.Options options)
    {
        var jsonOpts = new JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            WriteIndented = options.WriteIndented
        };

        JsonNode? node = BuildNode(obj, typeof(object), options);

        if (node is null) return "null";

        if (options.IncludeRootType && obj != null)
        {
            string rootType = SerializerCore.GetTypeCode(obj.GetType(), options);
            var rootWrapper = new JsonObject
            {
                ["$r"] = JsonValue.Create(rootType),
                ["$v"] = node
            };
            return rootWrapper.ToJsonString(jsonOpts);
        }

        return node.ToJsonString(jsonOpts);
    }

    public static string Serialize<T>(T obj, Serializer.Options options)
    {
        var jsonOpts = new JsonSerializerOptions
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            WriteIndented = options.WriteIndented
        };

        JsonNode? node = BuildNode(obj, typeof(T), options);
        if (node is null) return "null";

        if (options.IncludeRootType && obj != null)
        {
            string rootType = SerializerCore.GetTypeCode(obj.GetType(), options);
            var rootWrapper = new JsonObject
            {
                ["$r"] = JsonValue.Create(rootType),
                ["$v"] = node
            };
            return rootWrapper.ToJsonString(jsonOpts);
        }

        return node.ToJsonString(jsonOpts);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SERIALIZATION
    // ════════════════════════════════════════════════════════════════════════

    private static JsonNode? BuildNode(object? obj, Type declaredType, Serializer.Options options)
        => BuildNode(obj, declaredType, options, null, 0);

    private static JsonNode? BuildNode(object? obj, Type declaredType, Serializer.Options options,
        HashSet<object>? visiting, int depth)
    {
        if (obj is null) return null;

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
                    "The object graph contains a cycle that cannot be represented in JSON.");

            try
            {
                return BuildNodeCore(obj, declaredType, options, visiting, depth);
            }
            finally
            {
                visiting.Remove(obj);
            }
        }

        return BuildNodeCore(obj, declaredType, options, visiting, depth);
    }

    private static JsonNode? BuildNodeCore(object? obj, Type declaredType, Serializer.Options options,
        HashSet<object>? visiting, int depth)
    {
        if (obj is null) return null;

        Type actualType = obj.GetType();

        bool needTag = SerializerCore.NeedsTypeTag(actualType, declaredType);
        string? tag  = needTag ? SerializerCore.GetTypeCode(actualType, options) : null;

        JsonNode valueNode = BuildValueNode(obj, actualType, options, visiting, depth + 1);

        if (tag is null) return valueNode;

        return new JsonObject
        {
            ["$t"] = JsonValue.Create(tag),
            ["$v"] = valueNode,
        };
    }

    private static JsonNode BuildValueNode(object obj, Type actualType, Serializer.Options options,
        HashSet<object>? visiting, int depth)
    {
        if (SerializerCore.IsPrimitiveLike(actualType))
            return PrimitiveToNode(obj, actualType);

        if (obj is IDictionary dict)
            return DictToNode(dict, actualType, options, visiting, depth);

        if (obj is IEnumerable enumerable)
            return EnumerableToNode(enumerable, actualType, options, visiting, depth);

        return ObjectToNode(obj, actualType, options, visiting, depth);
    }

    private static JsonNode PrimitiveToNode(object obj, Type t)
    {
        if (t == typeof(TimeSpan)  || t == typeof(TimeSpan?))
            return JsonValue.Create(((TimeSpan)obj).ToString("c"))!;
        if (t == typeof(DateTime)  || t == typeof(DateTime?))
            return JsonValue.Create(((DateTime)obj).ToString("O"))!;
        if (t == typeof(DateTimeOffset) || t == typeof(DateTimeOffset?))
            return JsonValue.Create(((DateTimeOffset)obj).ToString("O"))!;
        if (t == typeof(Guid)      || t == typeof(Guid?))
            return JsonValue.Create(((Guid)obj).ToString())!;
        if (t == typeof(char)      || t == typeof(char?))
            return JsonValue.Create(obj.ToString())!;
        if (t == typeof(decimal)   || t == typeof(decimal?))
            return JsonValue.Create(obj.ToString())!;
        if (t == typeof(Uri))
            return JsonValue.Create(((Uri)obj).ToString())!;
        if (t == typeof(Version))
            return JsonValue.Create(((Version)obj).ToString())!;
        if (t.IsEnum)
            return JsonValue.Create(obj.ToString())!;

        return JsonValue.Create(obj)!;
    }

    private static JsonNode DictToNode(IDictionary dict, Type actualType, Serializer.Options options,
        HashSet<object>? visiting, int depth)
    {
        Type keyType   = actualType.IsGenericType ? actualType.GetGenericArguments()[0] : typeof(string);
        Type valueType = actualType.IsGenericType ? actualType.GetGenericArguments()[1] : typeof(object);

        if (keyType == typeof(string))
        {
            var obj = new JsonObject();
            foreach (DictionaryEntry kv in dict)
            {
                string key  = kv.Key?.ToString() ?? "null";
                obj[key] = BuildNode(kv.Value, valueType, options, visiting, depth);
            }
            return obj;
        }

        var array = new JsonArray();
        foreach (DictionaryEntry kv in dict)
        {
            var entry = new JsonObject
            {
                ["$k"] = BuildNode(kv.Key, keyType, options, visiting, depth),
                ["$v"] = BuildNode(kv.Value, valueType, options, visiting, depth)
            };
            array.Add(entry);
        }
        return array;
    }

    private static JsonArray EnumerableToNode(IEnumerable enumerable, Type actualType, Serializer.Options options,
        HashSet<object>? visiting, int depth)
    {
        Type elemType = actualType.IsArray
            ? actualType.GetElementType()!
            : actualType.IsGenericType
                ? actualType.GetGenericArguments()[0]
                : typeof(object);

        var arr = new JsonArray();
        foreach (object? item in enumerable)
            arr.Add(BuildNode(item, elemType, options, visiting, depth));
        return arr;
    }

    private static JsonObject ObjectToNode(object obj, Type actualType, Serializer.Options options,
        HashSet<object>? visiting, int depth)
    {
        var node = new JsonObject();
        foreach (var prop in SerializerCore.GetProperties(actualType))
        {
            object? val = prop.GetValue(obj);

            if (val is Type typeVal)
                node[prop.Name] = JsonValue.Create(typeVal.FullName);
            else
                node[prop.Name] = BuildNode(val, prop.PropertyType, options, visiting, depth);
        }
        return node;
    }
}
