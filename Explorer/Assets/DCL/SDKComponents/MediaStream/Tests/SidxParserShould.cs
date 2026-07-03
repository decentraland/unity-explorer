#if AV_PRO_PRESENT
using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace DCL.SDKComponents.MediaStream.Tests
{
    /// <summary>
    ///     Covers <see cref="SidxParser.TryParse"/> — the bit that walks a YouTube fmp4
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

        private readonly List<SidxParser.SegmentInfo> segments = new ();

        [SetUp]
        public void SetUp() => segments.Clear();

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

            bool ok = SidxParser.TryParse(box, ANCHOR_OFFSET, segments);

            Assert.That(ok, Is.True);
            Assert.That(segments.Count, Is.EqualTo(3));

            Assert.That(segments[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET));
            Assert.That(segments[0].ByteSize, Is.EqualTo(1_000));
            Assert.That(segments[0].DurationSeconds, Is.EqualTo(6.0).Within(1e-9));

            Assert.That(segments[1].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + 1_000));
            Assert.That(segments[1].ByteSize, Is.EqualTo(2_500));

            Assert.That(segments[2].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + 3_500));
            Assert.That(segments[2].ByteSize, Is.EqualTo(1_750));
            Assert.That(segments[2].DurationSeconds, Is.EqualTo(3.0).Within(1e-9));
        }

        [Test]
        public void Parse_NonZeroFirstOffset_ShiftsFirstFragment()
        {
            const long FIRST_OFFSET = 128;

            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: FIRST_OFFSET)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            bool ok = SidxParser.TryParse(box, ANCHOR_OFFSET, segments);

            Assert.That(ok, Is.True);
            Assert.That(segments[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + FIRST_OFFSET));
        }

        [Test]
        public void Parse_ReuseList_ClearsPreviousEntries()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            // Pre-fill the buffer with stale data — the parser must clear before filling.
            segments.Add(new SidxParser.SegmentInfo(byteOffset: 999, byteSize: 999, durationSeconds: 9));
            segments.Add(new SidxParser.SegmentInfo(byteOffset: 888, byteSize: 888, durationSeconds: 8));

            bool ok = SidxParser.TryParse(box, ANCHOR_OFFSET, segments);

            Assert.That(ok, Is.True);
            Assert.That(segments.Count, Is.EqualTo(1));
            Assert.That(segments[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET));
        }

        // -------------------------------------------------------------------------
        // Malformed input
        // -------------------------------------------------------------------------

        [Test]
        public void Parse_WrongBoxType_ReturnsFalse()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            // Overwrite the 'sidx' four-CC with 'moof' (0x6D6F6F66).
            box[4] = 0x6D;
            box[5] = 0x6F;
            box[6] = 0x6F;
            box[7] = 0x66;

            Assert.That(SidxParser.TryParse(box, ANCHOR_OFFSET, segments), Is.False);
            Assert.That(segments, Is.Empty);
        }

        [Test]
        public void Parse_TruncatedBuffer_ReturnsFalse()
        {
            byte[] tooShort = new byte[16];

            Assert.That(SidxParser.TryParse(tooShort, ANCHOR_OFFSET, segments), Is.False);
            Assert.That(segments, Is.Empty);
        }

        [Test]
        public void Parse_ZeroReferenceCount_ReturnsFalse()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: DEFAULT_TIMESCALE, firstOffset: 0)
                .Build();

            Assert.That(SidxParser.TryParse(box, ANCHOR_OFFSET, segments), Is.False);
            Assert.That(segments, Is.Empty);
        }

        [Test]
        public void Parse_ZeroTimescale_ReturnsFalse()
        {
            byte[] box = new SidxBoxBuilder(version: 0, timescale: 0, firstOffset: 0)
                .Add(refSize: 1_000, durationTicks: 6_000)
                .Build();

            Assert.That(SidxParser.TryParse(box, ANCHOR_OFFSET, segments), Is.False);
            Assert.That(segments, Is.Empty);
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

            bool ok = SidxParser.TryParse(box, ANCHOR_OFFSET, segments);

            Assert.That(ok, Is.True);
            Assert.That(segments.Count, Is.EqualTo(2));

            // First media fragment is unchanged.
            Assert.That(segments[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET));
            Assert.That(segments[0].ByteSize, Is.EqualTo(1_000));

            // Second media fragment must be shifted past the skipped nested-sidx region
            // (1_000 + 500), proving the offset accounting stayed coherent.
            Assert.That(segments[1].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + 1_500));
            Assert.That(segments[1].ByteSize, Is.EqualTo(2_500));
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

            bool ok = SidxParser.TryParse(box, ANCHOR_OFFSET, segments);

            Assert.That(ok, Is.True);
            Assert.That(segments[0].ByteOffset, Is.EqualTo(ANCHOR_OFFSET + FIRST_OFFSET_64));
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
#endif
