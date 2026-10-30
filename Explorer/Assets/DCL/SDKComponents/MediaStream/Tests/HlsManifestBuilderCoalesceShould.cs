using DCL.SDKComponents.MediaStream.YouTube;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace DCL.SDKComponents.MediaStream.Tests
{
    public class HlsManifestBuilderCoalesceShould
    {
        private static List<SidxParser.SegmentInfo> Fragments(int count, long size, double duration)
        {
            var fragments = new List<SidxParser.SegmentInfo>(count);
            long offset = 1000;

            for (var i = 0; i < count; i++)
            {
                fragments.Add(new SidxParser.SegmentInfo(offset, size, duration));
                offset += size;
            }

            return fragments;
        }

        [Test]
        public void MergeContiguousFragmentsUpToTargetDuration()
        {
            // 10 fragments × 5s → target 15s ⇒ groups of 3 (15s) + tail of 1 (5s)
            var merged = new List<SidxParser.SegmentInfo>();
            HlsManifestBuilder.Coalesce(Fragments(10, 100, 5), 15f, merged);

            Assert.That(merged, Has.Count.EqualTo(4));
            Assert.That(merged.Take(3).Select(s => s.ByteSize), Is.All.EqualTo(300));
            Assert.That(merged[3].ByteSize, Is.EqualTo(100));
            Assert.That(merged.Sum(s => s.ByteSize), Is.EqualTo(1000));
            Assert.That(merged.Sum(s => s.DurationSeconds), Is.EqualTo(50).Within(1e-6));
        }

        [Test]
        public void PreserveContiguousByteRanges()
        {
            var merged = new List<SidxParser.SegmentInfo>();
            HlsManifestBuilder.Coalesce(Fragments(6, 250, 4), 8f, merged);

            for (var i = 1; i < merged.Count; i++)
                Assert.That(merged[i].ByteOffset, Is.EqualTo(merged[i - 1].ByteOffset + merged[i - 1].ByteSize));

            Assert.That(merged[0].ByteOffset, Is.EqualTo(1000));
        }

        [Test]
        public void ClearTheResultBufferBeforeFilling()
        {
            var merged = new List<SidxParser.SegmentInfo> { new (999, 999, 999) };

            HlsManifestBuilder.Coalesce(Fragments(2, 100, 5), 60f, merged);

            Assert.That(merged, Has.Count.EqualTo(1));
            Assert.That(merged[0].ByteOffset, Is.EqualTo(1000));
        }

        [Test]
        public void KeepSingleGroupWhenTargetExceedsTotalDuration()
        {
            var merged = new List<SidxParser.SegmentInfo>();
            HlsManifestBuilder.Coalesce(Fragments(4, 100, 5), 60f, merged);

            Assert.That(merged, Has.Count.EqualTo(1));
            Assert.That(merged[0].ByteSize, Is.EqualTo(400));
        }
    }
}
