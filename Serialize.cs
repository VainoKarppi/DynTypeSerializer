
using DynTypeSerializer.Serialization;
using DynTypeSerializer.Serialization.Binary;
using DynTypeSerializer.Serialization.Json;

namespace DynTypeSerializer;

public static partial class Serializer
{
    // ════════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS — JSON (string)
    // ════════════════════════════════════════════════════════════════════════
    /// <summary>Serialize any object to a type-preserving JSON string.</summary>
    public static string SerializeToString(object? obj, Options? options = null)
    {
        options ??= new Options();
        LogSerialize(_logger, obj is null ? "null" : (obj.GetType().FullName ?? obj.GetType().Name));
        return JsonSerializerCore.Serialize(obj, options);
    }

    /// <summary>
    /// Serialize with a known declared type to a type-preserving JSON string.
    /// </summary>
    public static string SerializeToString<T>(T obj, Options? options = null)
    {
        options ??= new Options();
        LogSerialize(_logger, typeof(T).FullName ?? typeof(T).Name);
        return JsonSerializerCore.Serialize(obj, options);
    }

    /// <summary>Deserialize JSON back to T, restoring all dynamic types.</summary>
    public static T? Deserialize<T>(string json, Options? options = null)
    {
        options ??= new Options();
        LogDeserialize(_logger, typeof(T).FullName ?? typeof(T).Name);
        return JsonDeserializerCore.Deserialize<T>(json);
    }

    /// <summary>Deserialize JSON when the root type is unknown (returns object / boxed value).</summary>
    public static object? DeserializeDynamic(string json, Options? options = null)
    {
        options ??= new Options();
        LogDeserialize(_logger, "dynamic");
        return JsonDeserializerCore.DeserializeDynamic(json);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS — BINARY (byte[])
    // ════════════════════════════════════════════════════════════════════════
    /// <summary>Serialize any object to the compact binary format.</summary>
    public static byte[] SerializeToBytes(object? obj, Options? options = null)
    {
        options ??= new Options();
        LogSerialize(_logger, obj is null ? "null" : (obj.GetType().FullName ?? obj.GetType().Name));
        return BinarySerializer.Serialize(obj, options);
    }

    /// <summary>Serialize with a known declared type to the compact binary format.</summary>
    public static byte[] SerializeToBytes<T>(T obj, Options? options = null)
    {
        options ??= new Options();
        LogSerialize(_logger, typeof(T).FullName ?? typeof(T).Name);
        return BinarySerializer.Serialize(obj, options);
    }

    /// <summary>Deserialize compact binary data back to T.</summary>
    public static T? Deserialize<T>(byte[] data, Options? options = null)
        => Deserialize<T>((ReadOnlySpan<byte>)data, options);

    /// <summary>Deserialize compact binary data from a span back to T.</summary>
    public static T? Deserialize<T>(ReadOnlySpan<byte> data, Options? options = null)
    {
        options ??= new Options();
        LogDeserialize(_logger, typeof(T).FullName ?? typeof(T).Name);
        return BinaryDeserializer.Deserialize<T>(data, options);
    }

    /// <summary>Deserialize compact binary data when the root type is unknown.</summary>
    public static object? DeserializeDynamic(byte[] data, Options? options = null)
    {
        options ??= new Options();
        LogDeserialize(_logger, "dynamic");
        return BinaryDeserializer.DeserializeDynamic(data, options);
    }
}


