namespace DynTypeSerializer.Serialization.Binary;

/// <summary>
/// Header/version constants and helpers for the binary wire format.
/// </summary>
/// <remarks>
/// The exact byte layout is:
/// <code>
/// [Magic:   2 bytes] "DB"
/// [Version: 1 byte ] BinaryFormatVersion (currently 1)
/// [Flags:   1 byte ] reserved for optional feature flags (0 for now)
/// [Payload: ...    ]
/// </code>
/// The header lets <see cref="BinaryDeserializer"/> reject incompatible or
/// corrupted payloads up front with a clear exception.
/// </remarks>
internal static class BinaryFormat
{
    /// <summary>Current wire-format version. Bump ONLY on breaking changes.</summary>
    internal const byte Version = 1;

    /// <summary>Magic bytes that identify a DynTypeSerializer binary payload.</summary>
    internal static ReadOnlySpan<byte> Magic => new byte[] { (byte)'D', (byte)'B' };

    /// <summary>Total header size in bytes: 2 (magic) + 1 (version) + 1 (flags).</summary>
    internal const int HeaderSize = 4;

    /// <summary>
    /// Writes the standard header (magic + version + flags) to the writer.
    /// </summary>
    internal static void WriteHeader(BinaryWriter writer)
    {
        writer.WriteRawBytes(Magic);
        writer.WriteByte(Version);
        writer.WriteByte(0 /* flags */);
    }

    /// <summary>
    /// Validates and consumes the header. Throws <see cref="InvalidDataException"/>
    /// on a bad magic number or an unsupported version.
    /// </summary>
    internal static void ReadAndValidateHeader(ref BinaryReader reader)
    {
        if (reader.Remaining < HeaderSize)
            throw new InvalidDataException("Binary payload is too short to contain a header.");

        // Magic.
        foreach (byte expected in Magic)
        {
            if (reader.ReadByte() != expected)
                throw new InvalidDataException("Not a DynTypeSerializer binary payload (bad magic).");
        }

        // Version.
        byte version = reader.ReadByte();
        if (version != Version)
            throw new InvalidDataException(
                $"Unsupported binary format version {version}. Expected {Version}.");

        // Flags (reserved).
        reader.ReadByte();
    }
}
