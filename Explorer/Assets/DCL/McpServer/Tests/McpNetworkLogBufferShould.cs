using DCL.WebRequests.Analytics;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.McpServer.Tests
{
    public class McpNetworkLogBufferShould
    {
        private McpNetworkLogBuffer buffer = null!;

        [SetUp]
        public void Setup()
        {
            buffer = new McpNetworkLogBuffer();
        }

        private void AppendOk(string url, int status = 200) =>
            buffer.Append(url, "GET", status, "application/json", 100, 12.5, failed: false, failureReason: null);

        private void AppendFailed(string url) =>
            buffer.Append(url, "GET", 0, "application/octet-stream", 0, 5, failed: true, failureReason: "Cancelled");

        private List<McpNetworkLogBuffer.Entry> Copy(long sinceSeq = -1, bool failedOnly = false, int status = -1, int limit = 100)
        {
            var list = new List<McpNetworkLogBuffer.Entry>();
            buffer.CopyTo(list, sinceSeq, failedOnly, status, limit);
            return list;
        }

        [Test]
        public void ReportMinusOneLatestSeqWhenEmpty()
        {
            Assert.That(buffer.LatestSeq, Is.EqualTo(-1));
            Assert.That(Copy(), Is.Empty);
        }

        [Test]
        public void AssignMonotonicSequenceNumbers()
        {
            AppendOk("a");
            AppendOk("b");
            AppendOk("c");

            List<McpNetworkLogBuffer.Entry> entries = Copy();

            Assert.That(buffer.LatestSeq, Is.EqualTo(2));
            Assert.That(entries.ConvertAll(e => e.Seq), Is.EqualTo(new long[] { 0, 1, 2 }));
            Assert.That(entries.ConvertAll(e => e.Url), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void ReturnOnlyEntriesNewerThanSinceSeq()
        {
            AppendOk("a");
            AppendOk("b");
            AppendOk("c");

            List<McpNetworkLogBuffer.Entry> entries = Copy(sinceSeq: 0);

            Assert.That(entries.ConvertAll(e => e.Seq), Is.EqualTo(new long[] { 1, 2 }));
        }

        [Test]
        public void TreatBothTransportFailuresAndErrorStatusesAsUnsuccessfulUnderFailedOnly()
        {
            AppendOk("ok");
            AppendOk("notfound", 404);
            AppendFailed("boom");

            List<McpNetworkLogBuffer.Entry> entries = Copy(failedOnly: true);

            Assert.That(entries.ConvertAll(e => e.Url), Is.EqualTo(new[] { "notfound", "boom" }));
        }

        [Test]
        public void FilterByExactStatus()
        {
            AppendOk("a");
            AppendOk("b", 404);
            AppendOk("c", 404);

            List<McpNetworkLogBuffer.Entry> entries = Copy(status: 404);

            Assert.That(entries.ConvertAll(e => e.Url), Is.EqualTo(new[] { "b", "c" }));
        }

        [Test]
        public void KeepOnlyTheNewestLimitEntries()
        {
            for (var i = 0; i < 10; i++)
                AppendOk($"u{i}");

            List<McpNetworkLogBuffer.Entry> entries = Copy(limit: 3);

            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(entries.ConvertAll(e => e.Url), Is.EqualTo(new[] { "u7", "u8", "u9" }));
        }

        [Test]
        public void DropOldEntriesOnceCapacityIsExceededButKeepSequenceMonotonic()
        {
            // CAPACITY is 512; overrun it so the oldest entries are evicted.
            const int TOTAL = 600;

            for (var i = 0; i < TOTAL; i++)
                AppendOk($"u{i}");

            Assert.That(buffer.LatestSeq, Is.EqualTo(TOTAL - 1));

            List<McpNetworkLogBuffer.Entry> entries = Copy(limit: 1000);

            // The oldest surviving entry is TOTAL-512, and sinceSeq below that is clamped to what's available.
            Assert.That(entries[0].Seq, Is.EqualTo(TOTAL - 512));
            Assert.That(entries[^1].Seq, Is.EqualTo(TOTAL - 1));
        }
    }
}
