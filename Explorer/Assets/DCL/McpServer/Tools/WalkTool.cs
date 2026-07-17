using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Arch;

namespace DCL.McpServer.Tools
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

        public McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public string Description =>
            "Walk/jog/run the player in a camera-relative direction for a number of seconds through the real locomotion pipeline "
            + "(collisions apply). directionY is forward, directionX is strafe right. Returns the start and end positions.";

        public JObject InputSchema =>
            McpInputSchema.Object()
                          .Number("directionX", "Strafe axis: 1 right, -1 left.", required: true)
                          .Number("directionY", "Forward axis: 1 forward, -1 backward.", required: true)
                          .Number("seconds", "How long to hold the movement. Default 1, max 30.")
                          .String("kind", "Movement speed. Default jog.", enumValues: new[] { "walk", "jog", "run" })
                          .Boolean("jump", "Jump once at the start of the movement. Default false.")
                          .Build();

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
                ["startPosition"] = startPosition.ToVector(),
                ["endPosition"] = endPosition.ToVector(),
                ["distance"] = Math.Round(Vector3.Distance(startPosition, endPosition), 2),
                ["parcel"] = endPosition.ToParcel().ToParcel(),
            };

            return McpToolResult.Text(result.ToString(Formatting.Indented));
        }
    }
}
