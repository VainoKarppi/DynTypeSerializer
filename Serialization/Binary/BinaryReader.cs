using System.Buffers.Binary;
using System.Text;

namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Forward-only, zero-copy binary reader over a <see cref="ReadOnlySpan{T}"/>.
/// Mirrors <see cref="BinaryWriter"/>.
/// </summary>
/// <remarks>
/// Implemented as a <see langword="ref struct"/> holding a span so that no
/// allocation or copy occurs on deserialization. Because it is a ref struct,
/// it must be threaded through the recursion by <see langword="ref"/> (see
/// <see cref="BinaryDeserializer"/>).
///
/// **Safety:** every read validates that the remaining buffer has enough bytes
/// BEFORE reading. Length/count prefixes must be bounds-checked by callers.
/// </remarks>
internal ref struct BinaryReader
{
    private ReadOnlySpan<byte> _remaining;

    public BinaryReader(ReadOnlySpan<byte> data)
        => _remaining = data;

    /// <summary>Number of unread bytes.</summary>
    public int Remaining => _remaining.Length;

    // ── Type tokens ──────────────────────────────────────────────────────────
    public BinaryTypeCode ReadTypeCode()
        => (BinaryTypeCode)ReadByte();

    /// <summary>Returns the next type code without consuming it.</summary>
    public BinaryTypeCode PeekTypeCode()
    {
        if (_remaining.Length < 1)
            throw new InvalidDataException("Binary stream ended mid type-code.");
        return (BinaryTypeCode)_remaining[0];
    }

    // ── Raw primitives (little-endian) ───────────────────────────────────────
    public byte ReadByte()
    {
        if (_remaining.Length < 1)
            throw new InvalidDataException("Binary stream ended mid byte.");
        var b = _remaining[0];
        _remaining = _remaining[1..];
        return b;
    }

    public int ReadInt32Fixed()
    {
        if (_remaining.Length < 4)
            throw new InvalidDataException("Binary stream ended mid Int32.");
        var v = BinaryPrimitives.ReadInt32LittleEndian(_remaining);
        _remaining = _remaining[4..];
        return v;
    }

    public long ReadInt64Fixed()
    {
        if (_remaining.Length < 8)
            throw new InvalidDataException("Binary stream ended mid Int64.");
        var v = BinaryPrimitives.ReadInt64LittleEndian(_remaining);
        _remaining = _remaining[8..];
        return v;
    }

    public uint ReadUInt32Fixed()
    {
        if (_remaining.Length < 4)
            throw new InvalidDataException("Binary stream ended mid UInt32.");
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_remaining);
        _remaining = _remaining[4..];
        return v;
    }

    public ulong ReadUInt64Fixed()
    {
        if (_remaining.Length < 8)
            throw new InvalidDataException("Binary stream ended mid UInt64.");
        var v = BinaryPrimitives.ReadUInt64LittleEndian(_remaining);
        _remaining = _remaining[8..];
        return v;
    }

    // ── Varints ───────────────────────────────────────────────────────────────
    public uint ReadVarUInt32()
    {
        uint value = 0;
        int shift = 0;
        while (true)
        {
            if (_remaining.Length < 1 || shift > 35)
                throw new InvalidDataException("Malformed varint.");

            var b = _remaining[0];
            _remaining = _remaining[1..];

            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }
        return value;
    }

    public int ReadVarInt32()
    {
        uint zz = ReadVarUInt32();
        return (int)((zz >> 1) ^ (uint)-(int)(zz & 1));
    }

    public long ReadVarInt64()
    {
        ulong zz = 0;
        int shift = 0;
        while (true)
        {
            if (_remaining.Length < 1 || shift > 70)
                throw new InvalidDataException("Malformed varint.");

            var b = _remaining[0];
            _remaining = _remaining[1..];

            zz |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }
        // Un-ZigZag.
        return (long)((zz >> 1) ^ (ulong)-(long)(zz & 1));
    }

    public ulong ReadVarUInt64()
    {
        ulong value = 0;
        int shift = 0;
        while (true)
        {
            if (_remaining.Length < 1 || shift > 70)
                throw new InvalidDataException("Malformed varint.");

            var b = _remaining[0];
            _remaining = _remaining[1..];

            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }
        return value;
    }

    // ── UTF-8 string: varint length + bytes ───────────────────────────────────
    public string ReadString()
    {
        uint length = ReadVarUInt32();
        if (length > (uint)_remaining.Length)
            throw new InvalidDataException("String length exceeds remaining buffer.");

        var slice = _remaining[..(int)length];
        _remaining = _remaining[(int)length..];
        return Encoding.UTF8.GetString(slice);
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        if ((uint)length > (uint)_remaining.Length)
            throw new InvalidDataException("Requested byte count exceeds remaining buffer.");
        var slice = _remaining[..length];
        _remaining = _remaining[length..];
        return slice;
    }
}

