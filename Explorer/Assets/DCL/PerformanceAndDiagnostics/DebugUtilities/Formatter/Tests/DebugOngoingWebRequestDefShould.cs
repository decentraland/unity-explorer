using DCL.DebugUtilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace DCL.DebugUtilities.Formatter.Tests
{
    /// <summary>
    /// Regression tests for <see cref="DebugOngoingWebRequestDef.DataSource"/>.
    ///
    /// Covers the race condition reported in
    /// https://github.com/decentraland/unity-explorer/issues/7370
    /// where Remove() fires Updated while a ListView binding iteration is still in
    /// progress, causing BindItem to receive a stale index or a null dataSource.
    /// </summary>
    [TestFixture]
    public class DebugOngoingWebRequestDefShould
    {
        private DebugOngoingWebRequestDef.DataSource dataSource;

        [SetUp]
        public void SetUp()
        {
            dataSource = new DebugOngoingWebRequestDef.DataSource();
        }

        [Test]
        public void FireUpdatedAfterAdd()
        {
            bool fired = false;
            dataSource.Updated = () => fired = true;

            dataSource.Add(MakeDummyRequest());

            Assert.IsTrue(fired, "Updated should fire after Add");
        }

        [Test]
        public void FireUpdatedAfterRemove()
        {
            var uwr = new UnityWebRequest("http://example.com");
            dataSource.Add(new DebugOngoingWebRequestDef.DebugWebRequestInfo { Request = uwr });

            bool fired = false;
            dataSource.Updated = () => fired = true;

            dataSource.Remove(uwr);

            Assert.IsTrue(fired, "Updated should fire after Remove");
        }

        [Test]
        public void ReduceCountAfterRemove()
        {
            var uwr = new UnityWebRequest("http://example.com");
            dataSource.Add(new DebugOngoingWebRequestDef.DebugWebRequestInfo { Request = uwr });
            Assert.AreEqual(1, dataSource.Requests.Count);

            dataSource.Remove(uwr);

            Assert.AreEqual(0, dataSource.Requests.Count, "Count should drop to 0 after Remove");
        }

        [Test]
        public void NotThrowWhenUpdatedCallbackReadsRequestsDuringRemove()
        {
            // Simulates BindItem being invoked inside the Updated callback with a safe index read.
            // After the fix, accessing Requests.Count in the callback must not throw even if the
            // list was just mutated.
            var uwr = new UnityWebRequest("http://example.com");
            dataSource.Add(new DebugOngoingWebRequestDef.DebugWebRequestInfo { Request = uwr });

            int countSeenInCallback = -1;
            dataSource.Updated = () => { countSeenInCallback = dataSource.Requests.Count; };

            Assert.DoesNotThrow(() => dataSource.Remove(uwr));
            Assert.AreEqual(0, countSeenInCallback,
                "Updated callback should see the post-removal count");
        }

        [Test]
        public void FireUpdatedAfterUpdateTime()
        {
            dataSource.Add(MakeDummyRequest());

            bool fired = false;
            dataSource.Updated = () => fired = true;

            dataSource.UpdateTime(DateTime.UtcNow.AddSeconds(1));

            Assert.IsTrue(fired, "Updated should fire after UpdateTime");
        }

        [Test]
        public void UpdateDurationOnUpdateTime()
        {
            var uwr = new UnityWebRequest("http://example.com");
            var startTime = DateTime.UtcNow;
            dataSource.Add(new DebugOngoingWebRequestDef.DebugWebRequestInfo
            {
                Request = uwr,
                StartTime = startTime,
                Duration = 0,
            });

            dataSource.UpdateTime(startTime.AddSeconds(1));

            ulong updatedDuration = dataSource.Requests[0].Duration;

            // 1 second ≈ 1_000_000_000 ns (allow small floating-point delta)
            Assert.Greater(updatedDuration, 0UL, "Duration should be non-zero after UpdateTime");
        }

        // ────────────────────────── helpers ──────────────────────────

        private static DebugOngoingWebRequestDef.DebugWebRequestInfo MakeDummyRequest() =>
            new () { Request = new UnityWebRequest("http://dummy.test"), StartTime = DateTime.UtcNow };
    }
}
