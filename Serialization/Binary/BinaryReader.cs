using System.Buffers.Binary;
using System.Text;

namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Forward-only binary reader. Mirrors <see cref="BinaryWriter"/>.
/// </summary>
/// <remarks>
/// Implemented as a class (not a ref struct) so that position mutations are
/// shared across recursive deserialization calls without needing <c>ref</c>
/// parameters. Reads directly from a byte array — never via JSON.
///
/// **Safety:** every read validates that the remaining buffer has enough bytes
/// BEFORE reading. Length/count prefixes must be bounds-checked by callers.
/// </remarks>
internal sealed class BinaryReader
{
    private readonly byte[] _data;
    private int _position;

    public BinaryReader(ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
        _position = 0;
    }

    /// <summary>Number of unread bytes.</summary>
    public int Remaining => _data.Length - _position;

    // ── Type tokens ──────────────────────────────────────────────────────────
    public BinaryTypeCode ReadTypeCode()
        => (BinaryTypeCode)ReadByte();

    /// <summary>Returns the next type code without consuming it.</summary>
    public BinaryTypeCode PeekTypeCode()
    {
        if (Remaining < 1)
            throw new InvalidDataException("Binary stream ended mid type-code.");
        return (BinaryTypeCode)_data[_position];
    }

    // ── Raw primitives (little-endian) ───────────────────────────────────────
    public byte ReadByte()
    {
        if (Remaining < 1)
            throw new InvalidDataException("Binary stream ended mid byte.");
        return _data[_position++];
    }

    public int ReadInt32Fixed()
    {
        if (Remaining < 4)
            throw new InvalidDataException("Binary stream ended mid Int32.");
        int v = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_position, 4));
        _position += 4;
        return v;
    }

    public long ReadInt64Fixed()
    {
        if (Remaining < 8)
            throw new InvalidDataException("Binary stream ended mid Int64.");
        long v = BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_position, 8));
        _position += 8;
        return v;
    }

    public uint ReadUInt32Fixed()
    {
        if (Remaining < 4)
            throw new InvalidDataException("Binary stream ended mid UInt32.");
        uint v = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_position, 4));
        _position += 4;
        return v;
    }

    public ulong ReadUInt64Fixed()
    {
        if (Remaining < 8)
            throw new InvalidDataException("Binary stream ended mid UInt64.");
        ulong v = BinaryPrimitives.ReadUInt64LittleEndian(_data.AsSpan(_position, 8));
        _position += 8;
        return v;
    }

    // ── Varints ───────────────────────────────────────────────────────────────
    public uint ReadVarUInt32()
    {
        uint value = 0;
        int shift = 0;
        while (true)
        {
            if (Remaining < 1 || shift > 35)
                throw new InvalidDataException("Malformed varint.");

            byte b = _data[_position++];
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
            if (Remaining < 1 || shift > 70)
                throw new InvalidDataException("Malformed varint.");

            byte b = _data[_position++];
            zz |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }
        return (long)((zz >> 1) ^ (ulong)-(long)(zz & 1));
    }

    public ulong ReadVarUInt64()
    {
        ulong value = 0;
        int shift = 0;
        while (true)
        {
            if (Remaining < 1 || shift > 70)
                throw new InvalidDataException("Malformed varint.");

            byte b = _data[_position++];
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
        if (length > (uint)Remaining)
            throw new InvalidDataException("String length exceeds remaining buffer.");

        string s = Encoding.UTF8.GetString(_data, _position, (int)length);
        _position += (int)length;
        return s;
    }

    public ReadOnlySpan<byte> ReadBytes(int length)
    {
        if ((uint)length > (uint)Remaining)
            throw new InvalidDataException("Requested byte count exceeds remaining buffer.");
        var slice = _data.AsSpan(_position, length);
        _position += length;
        return slice;
    }
}

