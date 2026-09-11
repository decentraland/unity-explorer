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

            var timedOutBeforeLastSecond = false;

            for (var second = 0; second < StreamableLoadingDefaults.TIMEOUT - 1; second++)
                timedOutBeforeLastSecond |= intent.UpdatePlayTimeout(1f);

            Assert.IsFalse(timedOutBeforeLastSecond,
                "Must not report a timeout before StreamableLoadingDefaults.TIMEOUT seconds of elapsed time have accumulated.");

            var timedOutAtTimeoutSecond = intent.UpdatePlayTimeout(1f);

            Assert.IsTrue(timedOutAtTimeoutSecond,
                "Every call must add its dt to the running elapsed time, so the timeout fires once the accumulated total " +
                "reaches StreamableLoadingDefaults.TIMEOUT seconds.");
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
