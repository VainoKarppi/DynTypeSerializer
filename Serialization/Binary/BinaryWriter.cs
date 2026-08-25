using System.Buffers;
using System.Text;

namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Growable binary writer over <see cref="ArrayBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the ONLY place the binary serializer writes bytes. It must never
/// produce JSON. The implementation should write directly to an
/// <see cref="ArrayBufferWriter{T}"/> to avoid repeated array resizing.
/// </para>
/// <para>
/// All length prefixes / counts must be written as varints (see
/// <see cref="WriteVarUInt32"/>). Signed integers must be ZigZag encoded first
/// (see <see cref="WriteVarInt32"/>).
/// </para>
/// </remarks>
internal sealed class BinaryWriter
{
    private readonly ArrayBufferWriter<byte> _buffer;

    public BinaryWriter()
        => _buffer = new ArrayBufferWriter<byte>();

    public BinaryWriter(int initialCapacity)
        => _buffer = new ArrayBufferWriter<byte>(initialCapacity);

    /// <summary>The bytes written so far.</summary>
    public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    /// <summary>
    /// Returns the serialized payload as a single <see cref="T:byte[]"/>.
    /// </summary>
    /// <remarks>
    /// Callers should prefer <see cref="WrittenSpan"/> / <see cref="ToArray"/>
    /// and avoid extra copies where practical.
    /// </remarks>
    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();

    // ── Type tokens ──────────────────────────────────────────────────────────
    public void WriteTypeCode(BinaryTypeCode code)
    {
        _buffer.GetSpan(1)[0] = (byte)code;
        _buffer.Advance(1);
    }

    // ── Raw primitives ───────────────────────────────────────────────────────
    public void WriteByte(byte value)
    {
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);
    }

    public void WriteInt32Fixed(int value)
    {
        var span = _buffer.GetSpan(4);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(span, value);
        _buffer.Advance(4);
    }

    public void WriteInt64Fixed(long value)
    {
        var span = _buffer.GetSpan(8);
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(span, value);
        _buffer.Advance(8);
    }

    public void WriteUInt32Fixed(uint value)
    {
        var span = _buffer.GetSpan(4);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        _buffer.Advance(4);
    }

    public void WriteUInt64Fixed(ulong value)
    {
        var span = _buffer.GetSpan(8);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        _buffer.Advance(8);
    }

    // ── Varints (protobuf-style base-128, LSB first) ────────────────────────
    public void WriteVarUInt32(uint value)
    {
        while (value >= 0x80)
        {
            _buffer.GetSpan(1)[0] = (byte)(value | 0x80);
            _buffer.Advance(1);
            value >>= 7;
        }
        _buffer.GetSpan(1)[0] = (byte)value;
        _buffer.Advance(1);
    }

    // ZigZag: map signed ints to unsigned so small magnitudes compress well.
    public void WriteVarInt32(int value)
        => WriteVarUInt32((uint)((value << 1) ^ (value >> 31)));

    public void WriteVarInt64(long value)
    {
        // ZigZag for 64-bit.
        ulong zz = unchecked((ulong)((value << 1) ^ (value >> 63)));
        while (zz >= 0x80)
        {
            _buffer.GetSpan(1)[0] = (byte)(zz | 0x80);
            _buffer.Advance(1);
            zz >>= 7;
        }
        _buffer.GetSpan(1)[0] = (byte)zz;
        _buffer.Advance(1);
    }

    public void WriteVarUInt64(ulong value)
    {
        while (value >= 0x80)
        {
            _buffer.GetSpan(1)[0] = (byte)(value | 0x80);
            _buffer.Advance(1);
            value >>= 7;
        }
        _buffer.GetSpan(1)[0] = (byte)value;
        _buffer.Advance(1);
    }

    // ── UTF-8 string: varint length + bytes (no terminator, no JSON) ────────
    public void WriteString(string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        WriteVarUInt32((uint)byteCount);

        var span = _buffer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, span);
        _buffer.Advance(byteCount);
    }

    public void WriteRawBytes(ReadOnlySpan<byte> bytes)
    {
        var span = _buffer.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        _buffer.Advance(bytes.Length);
    }
}
