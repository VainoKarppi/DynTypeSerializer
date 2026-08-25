using System.Collections.Concurrent;

namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Maps runtime <see cref="Type"/> instances to compact numeric IDs so that
/// type names are not repeated throughout a serialized payload.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="BinarySerializer.Serialize"/> run owns an instance of this
/// registry. IDs are assigned in first-use order and written into the payload
/// (either inline via a type table or as ascending IDs) so the deserializer can
/// rebuild the mapping without the full type name on every occurrence.
/// </para>
/// <para>
/// The registry is per-payload: it must NOT persist IDs across calls, because
/// T:type IDs are only meaningful within a single payload.
/// </para>
/// </remarks>
internal sealed class BinaryTypeRegistry
{
    private readonly Dictionary<Type, uint> _typeToId = new();
    private readonly List<Type> _types = new();
    private uint _nextId;

    /// <summary>All types registered so far, indexed by their assigned ID.</summary>
    public IReadOnlyList<Type> Types => _types;

    /// <summary>
    /// Returns the ID for <paramref name="type"/>, registering it first if
    /// needed.
    /// </summary>
    public uint GetTypeId(Type type)
    {
        if (_typeToId.TryGetValue(type, out var id))
            return id;

        id = _nextId++;
        _typeToId[type] = id;
        _types.Add(type);
        return id;
    }

    /// <summary>Looks up a type by its ID, or returns <see langword="null"/>.</summary>
    public Type? GetType(uint id)
        => id < (uint)_types.Count ? _types[(int)id] : null;
}
