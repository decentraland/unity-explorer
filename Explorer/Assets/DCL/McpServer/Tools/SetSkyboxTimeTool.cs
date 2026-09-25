using Cysharp.Threading.Tasks;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SkyBox;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Fixes the skybox time of day, the same effect as dragging the sidebar's skybox time slider
    ///     (and disabling time progression so it doesn't drift afterwards).
    /// </summary>
    public class SetSkyboxTimeTool : McpTool
    {
        private const float DEFAULT_TIMEOUT_SEC = 5f;
        private const float MIN_TIMEOUT_SEC = 0.5f;
        private const float MAX_TIMEOUT_SEC = 15f;
        private const float SETTLE_EPSILON = 0.001f;
        private const int POLL_INTERVAL_MS = 50;

        private readonly SkyboxSettingsAsset skyboxSettings;

        public override string Name => "set_skybox_time";

        public override string Description =>
            "Fix the skybox time of day (hour and minute, 24h clock) and stop it from progressing further, "
            + "like a user dragging the sidebar's skybox time slider and turning off time progression. Waits "
            + "for the change to actually finish applying (the skybox interpolates rather than snapping "
            + "instantly, especially across a large time jump) before reporting settled.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("hour", "Hour, 0-23.", isRequired: true)
                  .Number("minute", "Minute, 0-59.", isRequired: true)
                  .Number("timeoutSec", "Seconds to wait for the transition to finish. Default 5, max 15.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public SetSkyboxTimeTool(SkyboxSettingsAsset skyboxSettings)
        {
            this.skyboxSettings = skyboxSettings;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("hour", out float hour) || !arguments.TryGetFloat("minute", out float minute))
                return McpToolResult.Error("hour and minute are required.");

            hour = Mathf.Clamp(hour, 0, 23);
            minute = Mathf.Clamp(minute, 0, 59);

            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            float totalMinutes = hour * 60f + minute;
            float normalized = Mathf.Clamp01(totalMinutes / SkyboxSettingsAsset.TOTAL_MINUTES_IN_DAY);

            skyboxSettings.IsUIControlled = true;
            skyboxSettings.TargetTimeOfDayNormalized = normalized;
            skyboxSettings.UIOverrideTimeOfDayNormalized = normalized;
            skyboxSettings.TimeOfDayNormalized = normalized;

            // Something else (e.g. a day-cycle/global-time state) can re-touch TimeOfDayNormalized or
            // TargetTimeOfDayNormalized on a later frame and reopen an interpolation away from what we
            // just set -- poll the live value instead of trusting the assignment above to stick, and
            // re-assert it each time it's found to have drifted.
            var settled = false;
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSec;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (Mathf.Abs(skyboxSettings.TimeOfDayNormalized - normalized) <= SETTLE_EPSILON)
                {
                    settled = true;
                    break;
                }

                skyboxSettings.TargetTimeOfDayNormalized = normalized;
                skyboxSettings.TimeOfDayNormalized = normalized;

                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }

            var result = new JObject
            {
                ["hour"] = (int) hour,
                ["minute"] = (int) minute,
                ["normalized"] = normalized,
                ["settled"] = settled,
            };

            return McpToolResult.Json(result);
        }
    }
}
