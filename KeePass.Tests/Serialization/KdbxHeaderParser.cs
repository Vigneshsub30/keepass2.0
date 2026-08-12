using System;
using System.IO;

namespace KeePass.Tests.Serialization
{
    /// <summary>
    /// Parses the outer binary header of a KDBX file without decrypting the body.
    /// Only the fields that are deterministic (non-random) are extracted; random
    /// fields (MasterSeed, EncryptionIV, KdfParameters) are skipped.
    /// </summary>
    internal static class KdbxHeaderParser
    {
        // Magic signatures (from KdbxFile.cs)
        internal const uint ExpectedSig1 = 0x9AA2D903;
        internal const uint ExpectedSig2 = 0xB54BFB67;

        // Outer header field IDs (from KdbxFile.KdbxHeaderFieldID enum)
        private const byte FieldEndOfHeader     = 0;
        private const byte FieldCipherID        = 2;
        private const byte FieldCompressionFlags = 3;
        // Fields 4, 5, 6, 7, 8, 9, 11 are random or version-specific — skipped

        /// <summary>
        /// Parses the outer KDBX header from raw file bytes and returns
        /// the non-random fields needed for round-trip comparison.
        /// </summary>
        /// <param name="kdbxBytes">Full or partial KDBX file content (at least the header).</param>
        /// <returns>Parsed header information.</returns>
        /// <exception cref="FormatException">Thrown when the magic bytes are invalid.</exception>
        internal static KdbxHeaderInfo Parse(byte[] kdbxBytes)
        {
            using (MemoryStream ms = new MemoryStream(kdbxBytes))
            using (BinaryReader br = new BinaryReader(ms))
            {
                // ── Fixed prefix (12 bytes) ───────────────────────────────────

                uint sig1 = br.ReadUInt32();  // little-endian
                uint sig2 = br.ReadUInt32();

                if (sig1 != ExpectedSig1 || sig2 != ExpectedSig2)
                    throw new FormatException(
                        $"Invalid KDBX magic: sig1=0x{sig1:X8} sig2=0x{sig2:X8}");

                // fileVersion: lower 16 bits = minor, upper 16 bits = major
                uint fileVersion = br.ReadUInt32();
                ushort minor = (ushort)(fileVersion & 0x0000FFFF);
                ushort major = (ushort)((fileVersion & 0xFFFF0000) >> 16);

                bool isV4x = major >= 4;

                // ── TLV header fields ─────────────────────────────────────────

                byte[] cipherIdBytes = null;
                uint compressionFlags = 0;

                while (true)
                {
                    byte fieldId = br.ReadByte();

                    int size;
                    if (isV4x)
                        size = (int)br.ReadUInt32();
                    else
                        size = (int)br.ReadUInt16();

                    byte[] data = size > 0 ? br.ReadBytes(size) : Array.Empty<byte>();

                    switch (fieldId)
                    {
                        case FieldEndOfHeader:
                            goto doneReadingHeader;

                        case FieldCipherID:
                            if (data.Length != 16)
                                throw new FormatException(
                                    $"CipherID must be 16 bytes; got {data.Length}");
                            cipherIdBytes = data;
                            break;

                        case FieldCompressionFlags:
                            if (data.Length != 4)
                                throw new FormatException(
                                    $"CompressionFlags must be 4 bytes; got {data.Length}");
                            compressionFlags = BitConverter.ToUInt32(data, 0);
                            break;

                        default:
                            // Skip all other fields (random and version-specific)
                            break;
                    }
                }

                doneReadingHeader:
                return new KdbxHeaderInfo
                {
                    Sig1 = sig1,
                    Sig2 = sig2,
                    MajorVersion = major,
                    MinorVersion = minor,
                    CipherIdBytes = cipherIdBytes ?? Array.Empty<byte>(),
                    CompressionFlags = compressionFlags
                };
            }
        }
    }

    /// <summary>
    /// Non-random outer header fields extracted from a KDBX file.
    /// </summary>
    internal struct KdbxHeaderInfo
    {
        /// <summary>First KDBX magic value (0x9AA2D903).</summary>
        public uint Sig1;
        /// <summary>Second KDBX magic value (0xB54BFB67).</summary>
        public uint Sig2;
        /// <summary>Major format version (4 for KDBX 4.x, 3 for KDBX 3.x).</summary>
        public ushort MajorVersion;
        /// <summary>Minor format version (e.g. 0 for 4.0, 1 for 4.1).</summary>
        public ushort MinorVersion;
        /// <summary>16-byte cipher UUID (e.g. AES-256-CBC or ChaCha20).</summary>
        public byte[] CipherIdBytes;
        /// <summary>Compression algorithm: 0=None, 1=GZip.</summary>
        public uint CompressionFlags;
    }
}
