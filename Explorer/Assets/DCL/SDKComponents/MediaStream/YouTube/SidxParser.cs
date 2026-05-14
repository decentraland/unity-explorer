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
            if (boxBytes == null || boxBytes.Length < 32)
                return null;

            int pos = 0;

            // Box header: size (4) + type (4). Skip largesize handling — sidx boxes are tiny.
            uint boxSize = ReadUInt32BE(boxBytes, ref pos);
            uint boxType = ReadUInt32BE(boxBytes, ref pos);

            if (boxType != BOX_TYPE_SIDX) return null;
            if (boxSize < 32 || boxSize > boxBytes.Length) return null;

            // Full-box header: version (1) + flags (3).
            byte version = boxBytes[pos];
            pos += 4;

            // reference_ID (4) — we don't need it.
            pos += 4;

            uint timescale = ReadUInt32BE(boxBytes, ref pos);
            if (timescale == 0) return null;

            // earliest_presentation_time (4 or 8) + first_offset (4 or 8).
            long firstOffset;

            if (version == 0)
            {
                pos += 4; // earliest_presentation_time
                firstOffset = ReadUInt32BE(boxBytes, ref pos);
            }
            else
            {
                pos += 8; // earliest_presentation_time
                firstOffset = (long)ReadUInt64BE(boxBytes, ref pos);
            }

            // reserved (2) + reference_count (2).
            pos += 2;

            if (pos + 2 > boxBytes.Length) return null;

            ushort referenceCount = ReadUInt16BE(boxBytes, ref pos);
            if (referenceCount == 0) return null;

            // Each reference is 12 bytes. Bail if the buffer is too short.
            if (pos + (referenceCount * 12) > boxBytes.Length) return null;

            var segments = new List<SegmentInfo>(referenceCount);
            long currentByteOffset = anchorOffset + firstOffset;

            for (int i = 0; i < referenceCount; i++)
            {
                // reference_type (1 bit) | referenced_size (31 bits)
                uint refSizeBits = ReadUInt32BE(boxBytes, ref pos);
                bool isNestedSidx = (refSizeBits & 0x80000000u) != 0;
                uint refSize = refSizeBits & 0x7FFFFFFFu;

                // subsegment_duration (4 bytes, in timescale units)
                uint subsegmentDuration = ReadUInt32BE(boxBytes, ref pos);

                // starts_with_SAP (1 bit) | SAP_type (3 bits) | SAP_delta_time (28 bits) — skipped.
                pos += 4;

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

        private static uint ReadUInt32BE(byte[] data, ref int pos)
        {
            uint v = ((uint)data[pos] << 24)
                     | ((uint)data[pos + 1] << 16)
                     | ((uint)data[pos + 2] << 8)
                     | data[pos + 3];
            pos += 4;
            return v;
        }

        private static ulong ReadUInt64BE(byte[] data, ref int pos)
        {
            ulong hi = ReadUInt32BE(data, ref pos);
            ulong lo = ReadUInt32BE(data, ref pos);
            return (hi << 32) | lo;
        }

        private static ushort ReadUInt16BE(byte[] data, ref int pos)
        {
            ushort v = (ushort)((data[pos] << 8) | data[pos + 1]);
            pos += 2;
            return v;
        }
    }
}
