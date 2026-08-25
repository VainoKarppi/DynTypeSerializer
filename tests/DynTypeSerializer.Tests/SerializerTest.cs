using System.Text;

namespace DynTypeSerializer.Tests;

/// <summary>Helpers for working with the unified byte[] API in tests.</summary>
internal static class SerializerTest
{
    /// <summary>Decodes JSON bytes back to a string.</summary>
    public static string Json(this byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>Encodes a JSON string into UTF-8 bytes for Deserialize.</summary>
    public static byte[] Bytes(this string json) => Encoding.UTF8.GetBytes(json);
}
