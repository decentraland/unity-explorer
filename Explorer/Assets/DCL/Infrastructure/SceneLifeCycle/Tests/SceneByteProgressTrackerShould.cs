using Arch.Core;
using ECS.SceneLifeCycle.Systems;
using NUnit.Framework;

namespace ECS.SceneLifeCycle.Tests
{
    [TestFixture]
    public class SceneByteProgressTrackerShould
    {
        private const float DT = 1f; // Large dt → alpha clamps to 1 → smoothing skipped, target adopted directly.
        private const float TOLERANCE = 0.0001f;

        private World world;
        private SceneByteProgressTracker tracker;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();
            tracker = new SceneByteProgressTracker();
        }

        [TearDown]
        public void TearDown()
        {
            tracker.Dispose();
            world.Dispose();
        }

        [Test]
        public void ReturnZeroWhenNothingRegistered()
        {
            Assert.That(tracker.ComputeAndClamp(0, DT), Is.EqualTo(0f));
        }

        [Test]
        public void IgnoreNonPositiveContentLengthOnRegister()
        {
            Entity e = world.Create();
            tracker.RegisterIfNew(e, 0);
            tracker.RegisterIfNew(e, -5);

            // Treated as unknown size: contributes UNKNOWN_ASSET_BYTES (1) to denominator only.
            Assert.That(tracker.ComputeAndClamp(1, DT), Is.EqualTo(0f).Within(TOLERANCE));
        }

        [Test]
        public void BeIdempotentOnSecondRegister()
        {
            Entity e = world.Create();
            tracker.RegisterIfNew(e, 1000);
            tracker.RegisterIfNew(e, 1000); // second call must not double-count

            tracker.CreditFinish(e);

            // 1000/1000 capped at MAX_IN_PROGRESS (0.99)
            Assert.That(tracker.ComputeAndClamp(1, DT), Is.EqualTo(0.99f).Within(TOLERANCE));
        }

        [Test]
        public void WeightKnownAndUnknownAssetsCorrectly()
        {
            Entity known = world.Create();
            Entity unknown = world.Create();

            tracker.RegisterIfNew(known, 999);

            // known finishes, unknown also finishes without ever being registered.
            tracker.CreditFinish(known);
            tracker.CreditFinish(unknown);

            // effectiveTotal = 999 (known) + 1 (unknown) = 1000; completed = 999 + 1 = 1000 → capped at 0.99.
            Assert.That(tracker.ComputeAndClamp(2, DT), Is.EqualTo(0.99f).Within(TOLERANCE));
        }

        [Test]
        public void CapBelowOneEvenWhenFullyCredited()
        {
            Entity e = world.Create();
            tracker.RegisterIfNew(e, 500);
            tracker.CreditFinish(e);

            Assert.That(tracker.ComputeAndClamp(1, DT), Is.LessThan(1f));
        }

        [Test]
        public void AccumulateInProgressWithinSingleFrame()
        {
            Entity e = world.Create();
            tracker.RegisterIfNew(e, 1000);
            tracker.AccumulateInProgress(0.5f, 1000);

            // 500/1000 = 0.5
            Assert.That(tracker.ComputeAndClamp(1, DT), Is.EqualTo(0.5f).Within(TOLERANCE));
        }

        [Test]
        public void ResetInProgressBetweenFrames()
        {
            Entity e = world.Create();
            tracker.RegisterIfNew(e, 1000);
            tracker.AccumulateInProgress(0.8f, 1000);
            tracker.ComputeAndClamp(1, DT); // first frame: 0.8

            // No new accumulate this frame → in-progress contribution is 0.
            Assert.That(tracker.ComputeAndClamp(1, DT), Is.EqualTo(0.8f).Within(TOLERANCE));
        }

        [Test]
        public void NeverDecreaseBetweenFrames()
        {
            Entity small = world.Create();
            Entity large = world.Create();

            tracker.RegisterIfNew(small, 100);
            tracker.CreditFinish(small);

            float frame1 = tracker.ComputeAndClamp(1, DT);
            Assert.That(frame1, Is.GreaterThan(0f));

            // A new large entity registers, total grows, percentage would dip without clamping.
            tracker.RegisterIfNew(large, 10000);

            float frame2 = tracker.ComputeAndClamp(2, DT);
            Assert.That(frame2, Is.GreaterThanOrEqualTo(frame1));
        }

        [Test]
        public void StaySilentWhenDeltaTimeIsZero()
        {
            Entity e = world.Create();
            tracker.RegisterIfNew(e, 1000);
            tracker.CreditFinish(e);

            // dt=0 → alpha=0 → Lerp returns starting value → max stays at 0.
            Assert.That(tracker.ComputeAndClamp(1, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void CreditDeathBalancesTotal()
        {
            Entity alive = world.Create();
            Entity dying = world.Create();

            tracker.RegisterIfNew(alive, 100);
            tracker.RegisterIfNew(dying, 900);

            tracker.CreditDeath(dying);
            tracker.CreditFinish(alive);

            // dying's 900 + alive's 100 = 1000 of 1000 → capped at 0.99.
            Assert.That(tracker.ComputeAndClamp(2, DT), Is.EqualTo(0.99f).Within(TOLERANCE));
        }

        [Test]
        public void TreatUnregisteredFinishAsUnknownSize()
        {
            Entity ghost = world.Create();
            tracker.CreditFinish(ghost); // never registered → completed += UNKNOWN_ASSET_BYTES (1)

            // totalAssetsToResolve=1 → effectiveTotal = 0 + 1*1 = 1, completed = 1, capped at 0.99.
            Assert.That(tracker.ComputeAndClamp(1, DT), Is.EqualTo(0.99f).Within(TOLERANCE));
        }
    }
}