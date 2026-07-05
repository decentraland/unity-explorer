using Arch.Core;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Mcp.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Mcp.Tools
{
    public class MoveToTool : IMcpTool
    {
        private const float MAX_DURATION_SEC = 30f;
        private const float COMPLETION_GRACE_SEC = 5f;

        private readonly IGlobalWorldActions globalWorldActions;
        private readonly World world;
        private readonly Entity playerEntity;

        public string Name => "move_to";

        public string Description =>
            "Move the player to a world-space position (x,y,z in meters; one parcel is 16x16m). Instant by default, or smooth over durationSec. "
            + "Optionally face a look-at target on arrival. For crossing to another scene prefer the teleport tool.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""x"": { ""type"": ""number"" },
                    ""y"": { ""type"": ""number"" },
                    ""z"": { ""type"": ""number"" },
                    ""lookAtX"": { ""type"": ""number"" },
                    ""lookAtY"": { ""type"": ""number"" },
                    ""lookAtZ"": { ""type"": ""number"" },
                    ""durationSec"": { ""type"": ""number"", ""description"": ""Seconds to move over; 0 (default) teleports instantly."" }
                },
                ""required"": [""x"", ""y"", ""z""]
            }";

        internal MoveToTool(IGlobalWorldActions globalWorldActions, World world, Entity playerEntity)
        {
            this.globalWorldActions = globalWorldActions;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y) || !arguments.TryGetFloat("z", out float z))
                return McpToolResult.Error("x, y and z world coordinates are required.");

            Vector3? lookAtTarget = null;

            if (arguments.TryGetFloat("lookAtX", out float lookAtX) && arguments.TryGetFloat("lookAtY", out float lookAtY) && arguments.TryGetFloat("lookAtZ", out float lookAtZ))
                lookAtTarget = new Vector3(lookAtX, lookAtY, lookAtZ);

            float durationSec = Mathf.Clamp(arguments.GetFloat("durationSec", 0f), 0f, MAX_DURATION_SEC);
            var targetPosition = new Vector3(x, y, z);

            await UniTask.SwitchToMainThread(ct);

            try
            {
                await globalWorldActions.MoveAndRotatePlayerAsync(targetPosition, lookAtTarget, lookAtTarget, durationSec, ct)
                                        .Timeout(TimeSpan.FromSeconds(durationSec + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException) { return McpToolResult.Error($"move_to did not complete within {durationSec + COMPLETION_GRACE_SEC}s."); }

            // Give the teleport/rotation systems a couple of frames to apply the intents before reading back.
            await UniTask.DelayFrame(2, cancellationToken: ct);

            Vector3 finalPosition = world.Get<CharacterTransform>(playerEntity).Position;

            var result = new JObject
            {
                ["position"] = McpJson.Vector(finalPosition),
                ["parcel"] = McpJson.Parcel(finalPosition.ToParcel()),
            };

            return McpToolResult.Text(result.ToString(Formatting.Indented));
        }
    }
}
