using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.McpServer.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Holds a movement input on the player for a duration via <see cref="McpEcsMovementOverride" />,
    ///     exercising the regular locomotion pipeline (velocity, collisions, jumps) instead of teleporting.
    /// </summary>
    public class WalkTool : McpTool
    {
        private const float MIN_SECONDS = 0.1f;
        private const float MAX_SECONDS = 30f;
        private const float COMPLETION_GRACE_SEC = 5f;

        private static readonly MovementKind[] ALLOWED_KINDS = { MovementKind.WALK, MovementKind.JOG, MovementKind.RUN };

        private readonly World world;
        private readonly Entity playerEntity;

        public override string Name => "walk";

        public override string Description =>
            "Walk/jog/run the player in a camera-relative direction for a number of seconds through the real locomotion pipeline "
            + "(collisions apply). directionY is forward, directionX is strafe right. Returns the start and end positions.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("directionX", "Strafe axis: 1 right, -1 left.", isRequired: true)
                  .Number("directionY", "Forward axis: 1 forward, -1 backward.", isRequired: true)
                  .Number("seconds", "How long to hold the movement. Default 1, max 30.")
                  .Enum("kind", "Movement speed. Default jog.", ALLOWED_KINDS)
                  .Boolean("jump", "Jump once at the start of the movement. Default false.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public WalkTool(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            var direction = new Vector2(arguments.GetFloat("directionX", 0f), arguments.GetFloat("directionY", 0f));

            if (direction == Vector2.zero)
                return McpToolResult.Error("directionX and directionY must not both be zero.");

            direction.Normalize();

            float seconds = Mathf.Clamp(arguments.GetFloat("seconds", 1f), MIN_SECONDS, MAX_SECONDS);
            bool jump = arguments.GetBool("jump", false);

            if (!arguments.TryGetEnum("kind", MovementKind.JOG, out MovementKind kind, ALLOWED_KINDS))
                return McpToolResult.Error("kind must be one of: walk, jog, run.");

            Vector3 startPosition = world.Get<CharacterTransform>(playerEntity).Position;

            // A newer walk preempts a pending one; the preempted awaiter completes as a finished (shortened) hold.
            UniTask<AsyncUnit> hold = McpRequest.SendAsync(world, playerEntity, new McpEcsMovementOverride
            {
                Axes = direction,
                Kind = kind,
                EndTime = UnityEngine.Time.time + seconds,
                JumpRequested = jump,
            }, AsyncUnit.Default);

            try
            {
                await hold.AttachExternalCancellation(ct)
                          .Timeout(TimeSpan.FromSeconds(seconds + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException)
            {
                await McpRequest.AbandonAsync<McpEcsMovementOverride>(world, playerEntity);
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

            return McpToolResult.Json(result);
        }
    }
}
