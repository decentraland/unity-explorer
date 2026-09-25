using Cysharp.Threading.Tasks;
using DCL.SkyBox;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Fixes the skybox at a time of day and waits for the interpolated transition to land there.
    /// </summary>
    public static class SkyboxTimeControl
    {
        private const float SETTLE_EPSILON = 0.001f;
        private const int POLL_INTERVAL_MS = 50;

        public static float Normalize(float hour, float minute) =>
            Mathf.Clamp01(((Mathf.Clamp(hour, 0, 23) * 60f) + Mathf.Clamp(minute, 0, 59)) / SkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

        /// <summary>Returns true once the live time of day matches <paramref name="normalized" />, false on timeout.</summary>
        public static async UniTask<bool> SetAndWaitAsync(SkyboxSettingsAsset skyboxSettings, float normalized, float timeoutSec, CancellationToken ct)
        {
            skyboxSettings.IsUIControlled = true;
            skyboxSettings.UIOverrideTimeOfDayNormalized = normalized;
            Assert(skyboxSettings, normalized);

            // Something else (e.g. a day-cycle/global-time state) can re-touch TimeOfDayNormalized or
            // TargetTimeOfDayNormalized on a later frame and reopen an interpolation away from what we
            // just set -- poll the live value instead of trusting the assignment above to stick, and
            // re-assert it each time it's found to have drifted.
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSec;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (Mathf.Abs(skyboxSettings.TimeOfDayNormalized - normalized) <= SETTLE_EPSILON)
                    return true;

                Assert(skyboxSettings, normalized);
                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }

            return false;
        }

        private static void Assert(SkyboxSettingsAsset skyboxSettings, float normalized)
        {
            skyboxSettings.TargetTimeOfDayNormalized = normalized;
            skyboxSettings.TimeOfDayNormalized = normalized;
        }
    }
}
