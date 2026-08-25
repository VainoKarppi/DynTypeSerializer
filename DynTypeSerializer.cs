
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynTypeSerializer.Serialization;



namespace DynTypeSerializer;


// *! AVAILABLE METHODS:
/*
    Serialize(object? obj, Options? options = null)        - Serialize any object to JSON string, preserving runtime type.
    Serialize<T>(T obj, Options? options = null)           - Serialize with known declared type, suppresses $t tag if runtime matches declared.
    Deserialize<T>(string json)                            - Deserialize JSON string back to T, restoring all dynamic types.
    DeserializeDynamic(string json)                        - Deserialize JSON when root type is unknown, returns object.
    ContainsRootType(string json)                          - Checks if JSON contains a root type ('$r') tag.
    GetRootType(string json)                               - Gets the root Type from JSON with 'IncludeRootType' option.
*/


/*
    {
    "Name": "Alice",
    "Age": 30,
    "Items": [
        {
            "$t": "i",
            "$v": 42
        },
        {
            "$t": "s",
            "$v": "hello"
        },
        null,
        {
        "$t": "oa",
        "$v": [
            {
                "$t": "s",
                "$v": "nested"
            },
            {
                "$t": "i",
                "$v": 123
            },
            null
        ]
        }
    ],
    "Flags": {
        "IsActive": {
            "$t": "b",
            "$v": true
        },
        "Score": {
            "$t": "d",
            "$v": 99.5
        }
    },
    "Test": "03:30:00",
    "Sub": null
    }
*/

/// <summary>
/// Provides a fully dynamic, type-preserving JSON serializer.
/// </summary>
/// <remarks>
/// <para>
/// Serialization follows these rules:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// If the runtime type exactly matches the declared (static) type, the value
/// is emitted without a <c>$t</c> type tag.
/// </description>
/// </item>
/// <item>
/// <description>
/// If the runtime type differs from the declared type, or the declared type
/// is <see cref="object"/> or an interface, the value is wrapped as
/// <c>{ "$t": "&lt;code&gt;", "$v": &lt;value&gt; }</c> so the deserializer can
/// determine the actual runtime type.
/// </description>
/// </item>
/// <item>
/// <description>
/// Properties of complex objects are always serialized, regardless of whether
/// the runtime and declared types match.
/// </description>
/// </item>
/// <item>
/// <description>
/// Primitive and value types supported natively by JSON are emitted as
/// <see cref="System.Text.Json.Nodes.JsonValue"/> leaves.
/// </description>
/// </item>
/// <item>
/// <description>
/// Serialization is intended to provide round-trip fidelity, such that
/// <c>Deserialize&lt;T&gt;(Serialize(x))</c> reconstructs <c>x</c> for all
/// supported types.
/// </description>
/// </item>
/// </list>
/// </remarks>
public static partial class Serializer
{
    /// <summary>
    /// Provides configuration options for the serializer.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Gets or sets a value indicating whether the root object's runtime
        /// type should be included in the serialized output.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to include the root runtime type;
        /// otherwise, <see langword="false"/>.
        /// The default is <see langword="false"/>.
        /// </value>
        public bool IncludeRootType { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether full assembly-qualified
        /// type information should be included in type identifiers.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to include full assembly information;
        /// otherwise, <see langword="false"/>.
        /// The default is <see langword="false"/>.
        /// </value>
        public bool IncludeFullAssemblyInfo { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the serialized JSON should
        /// be formatted with indentation.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to write indented JSON;
        /// otherwise, <see langword="false"/>.
        /// The default is <see langword="false"/>.
        /// </value>
        public bool WriteIndented { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum nesting depth allowed during serialization.
        /// </summary>
        /// <value>
        /// The maximum number of nested objects that can be serialized before
        /// an <see cref="InvalidOperationException"/> is thrown. Protects
        /// against pathological deep object graphs that would otherwise
        /// overflow the stack. The default is <c>512</c>.
        /// </value>
        public int MaxSerializationDepth { get; set; } = 512;
    }


 
 

    // ════════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ════════════════════════════════════════════════════════════════════════ 
    

    /// <summary>Checks if the JSON string contains a root type ('$r') tag. [Serialized with 'IncludeRootType' option]</summary>
    public static bool ContainsRootType(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object) return false;
        return root.TryGetProperty("$r", out _);
    }

    /// <summary>Gets the root <see cref="Type"/> from JSON string with 'IncludeRootType'.</summary>
    public static Type? GetRootType(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("JSON root is not an object.");

        if (root.TryGetProperty("$r", out var rProp))
        {
            string code = rProp.GetString() ?? throw new InvalidOperationException("$r type code was null.");
            return SerializerCore.ResolveType(code);
        }

        return typeof(object);
    }
}