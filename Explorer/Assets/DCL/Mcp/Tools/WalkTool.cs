using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Mcp.Components;
using DCL.Mcp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Arch;

namespace DCL.Mcp.Tools
{
    /// <summary>
    ///     Holds a movement input on the player for a duration via <see cref="McpMovementOverride" />,
    ///     exercising the regular locomotion pipeline (velocity, collisions, jumps) instead of teleporting.
    /// </summary>
    public class WalkTool : IMcpTool
    {
        private const float MIN_SECONDS = 0.1f;
        private const float MAX_SECONDS = 30f;
        private const float COMPLETION_GRACE_SEC = 5f;

        private readonly World world;
        private readonly Entity playerEntity;

        public string Name => "walk";

        public string Description =>
            "Walk/jog/run the player in a camera-relative direction for a number of seconds through the real locomotion pipeline "
            + "(collisions apply). directionY is forward, directionX is strafe right. Returns the start and end positions.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""directionX"": { ""type"": ""number"", ""description"": ""Strafe axis: 1 right, -1 left."" },
                    ""directionY"": { ""type"": ""number"", ""description"": ""Forward axis: 1 forward, -1 backward."" },
                    ""seconds"": { ""type"": ""number"", ""description"": ""How long to hold the movement. Default 1, max 30."" },
                    ""kind"": { ""type"": ""string"", ""enum"": [""walk"", ""jog"", ""run""], ""description"": ""Movement speed. Default jog."" },
                    ""jump"": { ""type"": ""boolean"", ""description"": ""Jump once at the start of the movement. Default false."" }
                },
                ""required"": [""directionX"", ""directionY""]
            }";

        public WalkTool(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            var direction = new Vector2(arguments.GetFloat("directionX", 0f), arguments.GetFloat("directionY", 0f));

            if (direction == Vector2.zero)
                return McpToolResult.Error("directionX and directionY must not both be zero.");

            direction.Normalize();

            float seconds = Mathf.Clamp(arguments.GetFloat("seconds", 1f), MIN_SECONDS, MAX_SECONDS);
            bool jump = arguments.GetBool("jump", false);

            MovementKind kind = arguments.GetString("kind", "jog") switch
                                {
                                    "walk" => MovementKind.WALK,
                                    "run" => MovementKind.RUN,
                                    _ => MovementKind.JOG,
                                };

            await UniTask.SwitchToMainThread(ct);

            // A newer walk preempts a pending one; release its awaiter before replacing the override.
            if (world.TryGet(playerEntity, out McpMovementOverride existingOverride))
                existingOverride.Completion?.TrySetResult();

            Vector3 startPosition = world.Get<CharacterTransform>(playerEntity).Position;
            var completion = new UniTaskCompletionSource();

            world.AddOrSet(playerEntity, new McpMovementOverride
            {
                Axes = direction,
                Kind = kind,
                EndTime = UnityEngine.Time.time + seconds,
                JumpRequested = jump,
                Completion = completion,
            });

            try
            {
                await completion.Task.AttachExternalCancellation(ct)
                                .Timeout(TimeSpan.FromSeconds(seconds + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException)
            {
                await UniTask.SwitchToMainThread();

                if (world.Has<McpMovementOverride>(playerEntity))
                    world.Remove<McpMovementOverride>(playerEntity);

                return McpToolResult.Error($"walk did not complete within {seconds + COMPLETION_GRACE_SEC}s (is the simulation paused?).");
            }

            await UniTask.SwitchToMainThread(ct);

            Vector3 endPosition = world.Get<CharacterTransform>(playerEntity).Position;

            var result = new JObject
            {
                ["startPosition"] = McpJson.Vector(startPosition),
                ["endPosition"] = McpJson.Vector(endPosition),
                ["distance"] = Math.Round(Vector3.Distance(startPosition, endPosition), 2),
                ["parcel"] = McpJson.Parcel(endPosition.ToParcel()),
            };

            return McpToolResult.Text(result.ToString(Formatting.Indented));
        }
    }
}
