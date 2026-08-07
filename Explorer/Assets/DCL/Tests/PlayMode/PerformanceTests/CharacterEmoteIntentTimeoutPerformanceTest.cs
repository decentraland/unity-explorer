using DCL.AvatarRendering.Emotes;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Profiling;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// The null-coalescing operator binds looser than addition, so `playTimeout?.ElapsedTime ?? 0 + dt` parses as
    /// `playTimeout?.ElapsedTime ?? (0 + dt)`: once playTimeout is non-null, dt is silently dropped and ElapsedTime
    /// freezes after the first call, so the timeout never fires. Verifies elapsed time accumulates linearly instead.
    /// </summary>
    [Category("Performance")]
    public class CharacterEmoteIntentTimeoutPerformanceTest
    {
        [Test]
        [Performance]
        public void PlayTimeout_AccumulatesLinearly_AndFires()
        {
            var intent = new CharacterEmoteIntent();
            int calls = 0;
            while (!intent.UpdatePlayTimeout(0.5f) && calls < 1000)
                calls++;

            Assert.Less(calls, 1000, "timeout never fired — ElapsedTime is not accumulating (precedence bug)");
            Assert.That(calls, Is.InRange(119, 122), $"expected ~120 accumulating steps, got {calls}");

            var intent2 = new CharacterEmoteIntent();
            int calls2 = 0;
            while (!intent2.UpdatePlayTimeout(1.0f) && calls2 < 1000)
                calls2++;

            Assert.That(calls2, Is.InRange(59, 61), $"expected ~60 accumulating steps, got {calls2}");

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            Measure.Method(() =>
                    {
                        var i = new CharacterEmoteIntent();
                        for (int k = 0; k < 130; k++) i.UpdatePlayTimeout(0.5f);
                    })
                   .WarmupCount(5).MeasurementCount(10).GC().Run();
            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Assert.AreEqual(0, gcBytes, $"UpdatePlayTimeout must be allocation-free, allocated {gcBytes} bytes");
        }
    }
}
