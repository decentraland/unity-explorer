using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers <see cref="SidxParser.Parse"/> — the bit that walks a YouTube fmp4
    ///     sidx box and yields per-fragment byte offsets / sizes / durations.
    ///
    ///     Sample boxes are built by hand with <see cref="SidxBoxBuilder"/> so the test
    ///     cases stay independent of any real network response.
    /// </summary>
    public class SidxParserShould
    {
        private const long ANCHOR_OFFSET = 5_000;
        private const uint DEFAULT_TIMESCALE = 1_000;
        private const uint BOX_TYPE_SIDX = 0x73696478;

        // -------------------------------------------------------------------------
        // Valid input
        // -------------------------------------------------------------------------

        [Test]
        public void Parse_ValidV0Sidx_ReturnsCorrectOffsetsSizesAndDurations()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Add(refSize: 2_500, durationTicks: 6_000)
                .Add(refSize: 1_750, durationTicks: 3_000)
                .Build();

            IReadOnlyList<SidxParser.SegmentInfo>? result = SidxParser.Parse(box, ANCHOR_OFFSET);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(3));

            Assert.That(result[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET));
            Assert.That(result[0].ByteSize, Is.EqualTo(1_000));
            Assert.That(result[0].DurationSeconds, Is.EqualTo(6.0).Within(1e-9));

            Assert.That(result[1].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + 1_000));
            Assert.That(result[1].ByteSize, Is.EqualTo(2_500));

            Assert.That(result[2].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + 3_500));
            Assert.That(result[2].ByteSize, Is.EqualTo(1_750));
            Assert.That(result[2].DurationSeconds, Is.EqualTo(3.0).Within(1e-9));
        }

        [Test]
        public void Parse_NonZeroFirstOffset_ShiftsFirstFragment()
        {
            const long FIRST_OFFSET = 128;

            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: FIRST_OFFSET)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            IReadOnlyList<SidxParser.SegmentInfo>? result = SidxParser.Parse(box, ANCHOR_OFFSET);

            Assert.That(result, Is.Not.Null);
            Assert.That(result![0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + FIRST_OFFSET));
        }

        // -------------------------------------------------------------------------
        // Malformed input
        // -------------------------------------------------------------------------

        [Test]
        public void Parse_WrongBoxType_ReturnsNull()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            // Overwrite the 'sidx' four-CC with 'moof' (0x6D6F6F66).
            box[4] = 0x6D;
            box[5] = 0x6F;
            box[6] = 0x6F;
            box[7] = 0x66;

            Assert.That(SidxParser.Parse(box, ANCHOR_OFFSET), Is.Null);
        }

        [Test]
        public void Parse_TruncatedBuffer_ReturnsNull()
        {
            byte[] tooShort = new byte[16];

            Assert.That(SidxParser.Parse(tooShort, ANCHOR_OFFSET), Is.Null);
        }

        [Test]
        public void Parse_ZeroReferenceCount_ReturnsNull()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Build();

            Assert.That(SidxParser.Parse(box, ANCHOR_OFFSET), Is.Null);
        }

        [Test]
        public void Parse_ZeroTimescale_ReturnsNull()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: 0, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            Assert.That(SidxParser.Parse(box, ANCHOR_OFFSET), Is.Null);
        }

        // -------------------------------------------------------------------------
        // Nested sidx handling
        // -------------------------------------------------------------------------

        [Test]
        public void Parse_NestedSidxEntries_AreSkippedButOffsetAccountingStaysCoherent()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Add(refSize: 500, durationTicks: 0, isNestedSidx: true)
                .Add(refSize: 2_500, durationTicks: 6_000)
                .Build();

            IReadOnlyList<SidxParser.SegmentInfo>? result = SidxParser.Parse(box, ANCHOR_OFFSET);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(2));

            // First media fragment is unchanged.
            Assert.That(result[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET));
            Assert.That(result[0].ByteSize, Is.EqualTo(1_000));

            // Second media fragment must be shifted past the skipped nested-sidx region
            // (1_000 + 500), proving the offset accounting stayed coherent.
            Assert.That(result[1].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + 1_500));
            Assert.That(result[1].ByteSize, Is.EqualTo(2_500));
        }

        // -------------------------------------------------------------------------
        // Version 1 layout (64-bit time + offset)
        // -------------------------------------------------------------------------

        [Test]
        public void Parse_Version1_ReadsFirstOffsetAs64Bit()
        {
            // Value chosen so the high 32 bits are non-zero — verifies 64-bit decoding
            // and proves we don't truncate to int32.
            const long FIRST_OFFSET_64 = (1L << 33) + 64L;

            byte[] box = new SidxBoxBuilder(version: 1, timescale: DEFAULT_TIMESCALE, firstOffset: FIRST_OFFSET_64)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            IReadOnlyList<SidxParser.SegmentInfo>? result = SidxParser.Parse(box, ANCHOR_OFFSET);

            Assert.That(result, Is.Not.Null);
            Assert.That(result![0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + FIRST_OFFSET_64));
        }

        // -------------------------------------------------------------------------
        // Builder helper — writes a valid sidx box big-endian
        // -------------------------------------------------------------------------

        private sealed class SidxBoxBuilder
        {
            private readonly byte version;
            private readonly uint timescale;
            private readonly long firstOffset;
            private readonly List<(uint refSize, uint durationTicks, bool isNestedSidx)> entries = new ();

            public SidxBoxBuilder(byte version, uint timescale, long firstOffset)
            {
                this.version = version;
                this.timescale = timescale;
                this.firstOffset = firstOffset;
            }

            public SidxBoxBuilder Add(uint refSize, uint durationTicks, bool isNestedSidx = false)
            {
                entries.Add((refSize, durationTicks, isNestedSidx));
                return this;
            }

            public byte[] Build()
            {
                // Layout matches ISO/IEC 14496-12 §8.16.3.
                int eptAndOffsetBytes = version == 0 ? 8 : 16;
                int headerBytes = 8 /*box*/ + 4 /*full-box*/ + 4 /*ref_id*/ + 4 /*timescale*/
                                  + eptAndOffsetBytes + 2 /*reserved*/ + 2 /*ref_count*/;
                int totalBytes = headerBytes + (entries.Count * 12);

                using var ms = new MemoryStream(totalBytes);
                using var w = new BinaryWriter(ms);

                WriteUInt32Be(w, (uint)totalBytes);
                WriteUInt32Be(w, BOX_TYPE_SIDX);

                w.Write(version);
                w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); // flags

                WriteUInt32Be(w, 0); // reference_ID
                WriteUInt32Be(w, timescale);

                if (version == 0)
                {
                    WriteUInt32Be(w, 0);                  // earliest_presentation_time (32-bit)
                    WriteUInt32Be(w, (uint)firstOffset);  // first_offset (32-bit)
                }
                else
                {
                    WriteUInt64Be(w, 0);                  // earliest_presentation_time (64-bit)
                    WriteUInt64Be(w, (ulong)firstOffset); // first_offset (64-bit)
                }

                WriteUInt16Be(w, 0);                       // reserved
                WriteUInt16Be(w, (ushort)entries.Count);   // reference_count

                foreach (var entry in entries)
                {
                    uint packed = entry.refSize & 0x7FFFFFFFu;
                    if (entry.isNestedSidx) packed |= 0x80000000u;

                    WriteUInt32Be(w, packed);
                    WriteUInt32Be(w, entry.durationTicks);
                    WriteUInt32Be(w, 0); // SAP info — unused by parser
                }

                w.Flush();
                return ms.ToArray();
            }

            private static void WriteUInt16Be(BinaryWriter w, ushort v)
            {
                w.Write((byte)(v >> 8));
                w.Write((byte)v);
            }

            private static void WriteUInt32Be(BinaryWriter w, uint v)
            {
                w.Write((byte)(v >> 24));
                w.Write((byte)(v >> 16));
                w.Write((byte)(v >> 8));
                w.Write((byte)v);
            }

            private static void WriteUInt64Be(BinaryWriter w, ulong v)
            {
                WriteUInt32Be(w, (uint)(v >> 32));
                WriteUInt32Be(w, (uint)v);
            }
        }
    }
}
