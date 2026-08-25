namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Compact single-byte type tokens for the binary format.
/// </summary>
/// <remarks>
/// <para>
/// These values are an implementation detail of the binary wire format. They
/// must remain stable once <c>BinaryFormatVersion = 1</c> is frozen, so do not
/// reorder or reuse values after release.
/// </para>
/// <para>
/// **Decision (pending confirmation):** smaller integral types
/// (<c>byte</c>/<c>sbyte</c>/<c>short</c>/<c>ushort</c>) are promoted into the
/// signed/unsigned varint codes below rather than given their own token. This
/// keeps the type-code table small and leverages varint/ZigZag encoding, which
/// is efficient for small values anyway. <c>char</c> and <c>enum</c> receive
/// their own codes because they are not numerically interchangeable with the
/// integer codes during deserialization.
/// </para>
/// </remarks>
internal enum BinaryTypeCode : byte
{
    // ── Scalar / leaf tokens ────────────────────────────────────────────────
    Null = 0x00,

    False = 0x01,
    True = 0x02,

    // Signed integers: varint(ZigZag(value)). Covers sbyte, short, int, long.
    Int = 0x10,

    // Unsigned integers: varint(value). Covers byte, ushort, uint, ulong.
    UInt = 0x11,

    Char = 0x12,

    Float = 0x20,
    Double = 0x21,
    Decimal = 0x22,

    String = 0x30,

    // ── Collection tokens ───────────────────────────────────────────────────
    Array = 0x40,
    List = 0x41,
    Dictionary = 0x42,

    // ── Object / type tokens ────────────────────────────────────────────────
    Object = 0x50,

    TypeReference = 0x60
}
