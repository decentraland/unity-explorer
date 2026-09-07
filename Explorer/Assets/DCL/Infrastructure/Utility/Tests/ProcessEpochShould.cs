using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;

namespace Utility.Tests
{
    /// <summary>
    ///     Pins the process-start epoch every golden-harness real-time gate measures from: resolvable from
    ///     /proc alone, so a static initializer reading it stays safe under IL2CPP, and equal to the
    ///     runtime's own notion of the start time where the runtime has one.
    /// </summary>
    public class ProcessEpochShould
    {
        private const string PROC_STAT = "cpu  1 2 3 4\nintr 5\nctxt 6\nbtime 1700000000\nprocesses 7\n";

        // comm ("odd (comm) name") carries spaces and parentheses; starttime is 123456 ticks = 1234.56 s
        private const string SELF_STAT =
            "4242 (odd (comm) name) S 1 4242 4242 0 -1 4194560 100 0 0 0 5 3 0 0 20 0 7 0 123456 1000 200 18446744073709551615 1 2 3 4 5 6 0 0 0 0 0 0 17 3 0 0 0 0 0 0 0 0 0 0 0 0 0\n";

        [Test]
        public void ParseLinuxProcFilesPastAnAwkwardComm()
        {
            Assert.IsTrue(ProcessEpoch.TryParseLinuxProcStart(SELF_STAT, PROC_STAT, out DateTime start));

            DateTime expected = DateTime.UnixEpoch.AddSeconds(1700000000 + 1234.56);
            Assert.That((start - expected).Duration(), Is.LessThan(TimeSpan.FromMilliseconds(1)));
            Assert.AreEqual(DateTimeKind.Utc, start.Kind);
        }

        [TestCase("")]
        [TestCase("4242 (comm) S 1 4242")]
        [TestCase("4242 (comm) S 1 4242 4242 0 -1 4194560 100 0 0 0 5 3 0 0 20 0 7 0 notanumber 1000")]
        public void RejectAMalformedSelfStat(string selfStat) =>
            Assert.IsFalse(ProcessEpoch.TryParseLinuxProcStart(selfStat, PROC_STAT, out _));

        [TestCase("cpu  1 2 3 4\nprocesses 7\n")]
        [TestCase("cpu  1 2 3 4\nxbtime 1700000000\n")]
        [TestCase("btime notanumber\n")]
        public void RejectAStatWithoutABootTime(string stat) =>
            Assert.IsFalse(ProcessEpoch.TryParseLinuxProcStart(SELF_STAT, stat, out _));

        [Test]
        public void MatchTheRuntimeProcessStartOnLinux()
        {
            if (!File.Exists("/proc/self/stat"))
                Assert.Ignore("/proc is Linux-only");

            DateTime runtimeStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();

            Assert.That((ProcessEpoch.StartUtc - runtimeStart).Duration(), Is.LessThan(TimeSpan.FromSeconds(2)));
            Assert.That(ProcessEpoch.StartUtc, Is.LessThanOrEqualTo(DateTime.UtcNow));
        }
    }
}
