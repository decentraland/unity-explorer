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

            float normalized = SkyboxTimeControl.Normalize(hour, minute);
            bool settled = await SkyboxTimeControl.SetAndWaitAsync(skyboxSettings, normalized, timeoutSec, ct);

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
