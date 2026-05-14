using System.Collections.Generic;

namespace DCL.SDKComponents.MediaStream.YouTube
{
    /// <summary>
    ///     Parses the ISO/IEC 14496-12 §8.16.3 sidx (Segment Index) box. YouTube's adaptive
    ///     format URLs expose the sidx via the <c>indexRange</c> byte range; parsing it gives us
    ///     the offset, size and duration of each fmp4 fragment in the file. We use that to
    ///     synthesize a multi-segment HLS playlist so AVPro can start playback after fetching
    ///     just the first ~6-10s chunk instead of waiting for the entire byte range.
    ///
    ///     Single-pass, allocation-light, big-endian throughout (mp4 boxes are network byte
    ///     order). Returns <c>null</c> on any structural anomaly — caller falls back to the
    ///     legacy single-segment playlist behavior.
    /// </summary>
    internal static class SidxParser
    {
        // 'sidx' four-CC in big-endian uint32.
        private const uint BOX_TYPE_SIDX = 0x73696478;

        // Field sizes in bytes (ISO/IEC 14496-12 §8.16.3).
        private const int UINT16_BYTES = 2;
        private const int UINT32_BYTES = 4;
        private const int UINT64_BYTES = 8;
        private const int BOX_HEADER_BYTES = 8;             // size (4) + type (4)
        private const int FULL_BOX_HEADER_BYTES = 4;        // version (1) + flags (3)
        private const int REFERENCE_ID_BYTES = 4;
        private const int RESERVED_BYTES = 2;
        private const int EPT_AND_FIRST_OFFSET_BYTES_V0 = 8; // 4 + 4 (32-bit variant)
        private const int REFERENCE_ENTRY_BYTES = 12;       // reference (4) + duration (4) + SAP (4)

        // Minimum legal sidx box: version=0 with reference_count=0.
        private const int MIN_SIDX_BOX_BYTES = BOX_HEADER_BYTES
                                               + FULL_BOX_HEADER_BYTES
                                               + REFERENCE_ID_BYTES
                                               + UINT32_BYTES          // timescale
                                               + EPT_AND_FIRST_OFFSET_BYTES_V0
                                               + RESERVED_BYTES
                                               + UINT16_BYTES;          // reference_count

        // Per-reference 32-bit packed field: 1-bit reference_type | 31-bit referenced_size.
        private const uint NESTED_SIDX_FLAG_MASK = 0x80000000u;
        private const uint REFERENCED_SIZE_MASK = 0x7FFFFFFFu;

        // Big-endian byte shifts.
        private const int BITS_PER_BYTE = 8;
        private const int UINT32_BITS = 32;

        public readonly struct SegmentInfo
        {
            public readonly long ByteOffset;
            public readonly long ByteSize;
            public readonly double DurationSeconds;

            public SegmentInfo(long byteOffset, long byteSize, double durationSeconds)
            {
                ByteOffset = byteOffset;
                ByteSize = byteSize;
                DurationSeconds = durationSeconds;
            }
        }

        /// <summary>
        ///     Parses the sidx box contained in <paramref name="boxBytes"/>. The <paramref name="anchorOffset"/>
        ///     is the absolute file offset right after the sidx box ends (i.e. <c>IndexRangeEnd + 1</c>).
        ///     The sub-segment offsets in the result are absolute file offsets, ready to plug into
        ///     <c>#EXT-X-BYTERANGE</c>.
        /// </summary>
        public static IReadOnlyList<SegmentInfo>? Parse(byte[] boxBytes, long anchorOffset)
        {
            if (boxBytes.Length < MIN_SIDX_BOX_BYTES)
                return null;

            int pos = 0;

            // Box header: size + type. Skip largesize handling — sidx boxes are tiny.
            uint boxSize = ReadUInt32Be(boxBytes, ref pos);
            uint boxType = ReadUInt32Be(boxBytes, ref pos);

            if (boxType != BOX_TYPE_SIDX) return null;
            if (boxSize < MIN_SIDX_BOX_BYTES || boxSize > boxBytes.Length) return null;

            // Full-box header: version (1 byte) + flags (3 bytes); only version is needed.
            byte version = boxBytes[pos];
            pos += FULL_BOX_HEADER_BYTES;

            // reference_ID — we don't need it.
            pos += REFERENCE_ID_BYTES;

            uint timescale = ReadUInt32Be(boxBytes, ref pos);
            if (timescale == 0) return null;

            // earliest_presentation_time + first_offset (size depends on version).
            long firstOffset;

            if (version == 0)
            {
                pos += UINT32_BYTES; // earliest_presentation_time
                firstOffset = ReadUInt32Be(boxBytes, ref pos);
            }
            else
            {
                pos += UINT64_BYTES; // earliest_presentation_time
                firstOffset = (long)ReadUInt64Be(boxBytes, ref pos);
            }

            // reserved (2) + reference_count (2).
            pos += RESERVED_BYTES;

            if (pos + UINT16_BYTES > boxBytes.Length) return null;

            ushort referenceCount = ReadUInt16Be(boxBytes, ref pos);
            if (referenceCount == 0) return null;

            // Bail if the buffer can't hold every reference entry.
            if (pos + (referenceCount * REFERENCE_ENTRY_BYTES) > boxBytes.Length) return null;

            var segments = new List<SegmentInfo>(referenceCount);
            long currentByteOffset = anchorOffset + firstOffset;

            for (int i = 0; i < referenceCount; i++)
            {
                // reference_type (1 bit) | referenced_size (31 bits)
                uint refSizeBits = ReadUInt32Be(boxBytes, ref pos);
                bool isNestedSidx = (refSizeBits & NESTED_SIDX_FLAG_MASK) != 0;
                uint refSize = refSizeBits & REFERENCED_SIZE_MASK;

                // subsegment_duration (in timescale units).
                uint subsegmentDuration = ReadUInt32Be(boxBytes, ref pos);

                // starts_with_SAP (1 bit) | SAP_type (3 bits) | SAP_delta_time (28 bits) — skipped.
                pos += UINT32_BYTES;

                if (isNestedSidx)
                {
                    // Nested sidx references aren't expected from YouTube; skip them but keep the
                    // running offset coherent so subsequent media references still resolve.
                    currentByteOffset += refSize;
                    continue;
                }

                if (refSize == 0) continue;

                double durationSeconds = (double)subsegmentDuration / timescale;
                segments.Add(new SegmentInfo(currentByteOffset, refSize, durationSeconds));

                currentByteOffset += refSize;
            }

            return segments.Count == 0 ? null : segments;
        }

        private static uint ReadUInt32Be(byte[] data, ref int pos)
        {
            uint v = ((uint)data[pos] << (BITS_PER_BYTE * 3))
                     | ((uint)data[pos + 1] << (BITS_PER_BYTE * 2))
                     | ((uint)data[pos + 2] << BITS_PER_BYTE)
                     | data[pos + 3];
            pos += UINT32_BYTES;
            return v;
        }

        private static ulong ReadUInt64Be(byte[] data, ref int pos)
        {
            ulong hi = ReadUInt32Be(data, ref pos);
            ulong lo = ReadUInt32Be(data, ref pos);
            return (hi << UINT32_BITS) | lo;
        }

        private static ushort ReadUInt16Be(byte[] data, ref int pos)
        {
            ushort v = (ushort)((data[pos] << BITS_PER_BYTE) | data[pos + 1]);
            pos += UINT16_BYTES;
            return v;
        }
    }
}
