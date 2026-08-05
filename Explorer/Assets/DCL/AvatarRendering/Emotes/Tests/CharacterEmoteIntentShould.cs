using ECS.StreamableLoading;
using NUnit.Framework;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class CharacterEmoteIntentShould
    {
        [Test]
        public void UpdatePlayTimeout_AccumulateElapsedTimeAcrossCalls_ReturnTrueOnceTotalReachesTimeout()
        {
            var intent = new CharacterEmoteIntent();

            // One call per simulated second, mirroring how ConsumeEmoteIntent ticks this every frame with that frame's dt.
            var timedOutBeforeLastSecond = false;

            for (var second = 0; second < StreamableLoadingDefaults.TIMEOUT - 1; second++)
                timedOutBeforeLastSecond |= intent.UpdatePlayTimeout(1f);

            Assert.IsFalse(timedOutBeforeLastSecond,
                "Must not report a timeout before StreamableLoadingDefaults.TIMEOUT seconds of elapsed time have accumulated.");

            var timedOutAtTimeoutSecond = intent.UpdatePlayTimeout(1f);

            Assert.IsTrue(timedOutAtTimeoutSecond,
                "Elapsed time must accumulate across calls (each call's dt added to the running total) so IsTimeout fires " +
                "once the total reaches StreamableLoadingDefaults.TIMEOUT seconds. This is the #6531 unstuck watchdog for a " +
                "stranded CharacterEmoteIntent (see emote-lock-after-fish-catch). Fails at the pin because " +
                "`playTimeout?.ElapsedTime ?? 0 + dt` parses as `?? (0 + dt)`: once playTimeout is non-null the right side " +
                "of `??` is never evaluated, so ElapsedTime is reassigned to itself and freezes at the first call's dt " +
                "forever, and IsTimeout never fires.");
        }

        [Test]
        public void UpdatePlayTimeout_ReturnFalse_OnFirstCallBeforeTimeoutElapsed()
        {
            var intent = new CharacterEmoteIntent();

            var result = intent.UpdatePlayTimeout(1f);

            Assert.IsFalse(result);
        }
    }
}
