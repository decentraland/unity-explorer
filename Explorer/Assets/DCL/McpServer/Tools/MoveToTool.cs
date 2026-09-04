using Arch.Core;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Tools
{
    public class MoveToTool : McpTool
    {
        private const float MAX_DURATION_SEC = 30f;
        private const float COMPLETION_GRACE_SEC = 5f;

        /// <summary>Frames the teleport/rotation systems get to apply the intents before the pose is read back.</summary>
        private const int APPLY_DELAY_FRAMES = 2;

        /// <summary>
        ///     Seconds the camera gets to consume the look-at. It normally happens within a frame; a scene-controlled
        ///     camera never consumes it, which is what the deadline turns into a warning.
        /// </summary>
        private const float CAMERA_LOOK_AT_DEADLINE_SEC = 1f;

        private const string CAMERA_LOOK_AT_NOT_APPLIED =
            "the camera did not apply the look-at (a scene controls the camera, e.g. an SDK virtual camera); the player was moved — use look_at once the scene releases the camera";

        private readonly IGlobalWorldActions globalWorldActions;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;

        public override string Name => "move_to";

        public override string Description =>
            "Move the player to a world-space position (x,y,z in meters; one parcel is 16x16m). Instant by default, or smooth over durationSec. "
            + "lookAtX/Y/Z aim the camera and the avatar at that point — the same look-at a scene's movePlayerTo gives — and the result reports "
            + "the camera's aimErrorDegrees (a production look-at, not refined; use look_at when the aim must be exact). "
            + "For crossing to another scene prefer the teleport tool.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("x", isRequired: true)
                  .Number("y", isRequired: true)
                  .Number("z", isRequired: true)
                  .Number("lookAtX", "World point to aim the camera and avatar at on arrival; pass all three lookAt coordinates or none.")
                  .Number("lookAtY")
                  .Number("lookAtZ")
                  .Number("durationSec", "Seconds to move over; 0 (default) teleports instantly.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public MoveToTool(IGlobalWorldActions globalWorldActions, World world, Entity playerEntity, ExposedCameraData exposedCameraData)
        {
            this.globalWorldActions = globalWorldActions;
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetFloat("x", out float x) || !arguments.TryGetFloat("y", out float y) || !arguments.TryGetFloat("z", out float z))
                return McpToolResult.Error("x, y and z world coordinates are required." + arguments.NonNumericHint("x", "y", "z"));

            bool hasLookAtX = arguments.TryGetFloat("lookAtX", out float lookAtX);
            bool hasLookAtY = arguments.TryGetFloat("lookAtY", out float lookAtY);
            bool hasLookAtZ = arguments.TryGetFloat("lookAtZ", out float lookAtZ);
            bool hasLookAt = hasLookAtX && hasLookAtY && hasLookAtZ;

            if ((hasLookAtX || hasLookAtY || hasLookAtZ) && !hasLookAt)
                return McpToolResult.Error("lookAtX, lookAtY and lookAtZ must be provided together." + arguments.NonNumericHint("lookAtX", "lookAtY", "lookAtZ"));

            Vector3? lookAtTarget = hasLookAt ? new Vector3(lookAtX, lookAtY, lookAtZ) : null;
            float durationSec = Mathf.Clamp(arguments.GetFloat("durationSec", 0f), 0f, MAX_DURATION_SEC);
            var targetPosition = new Vector3(x, y, z);

            // The same two calls, in the same order, as the SDK's movePlayerTo: the camera look-at is its own intent
            // on the camera entity, MoveAndRotatePlayerAsync only moves and turns the avatar.
            if (lookAtTarget != null)
                globalWorldActions.RotateCamera(lookAtTarget, targetPosition);

            try
            {
                await globalWorldActions.MoveAndRotatePlayerAsync(targetPosition, lookAtTarget, lookAtTarget, durationSec, ct)
                                        .Timeout(TimeSpan.FromSeconds(durationSec + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException) { return McpToolResult.Error($"move_to did not complete within {durationSec + COMPLETION_GRACE_SEC}s."); }

            bool cameraLookAtApplied = lookAtTarget == null || await WaitForCameraLookAtAsync(ct);

            await UniTask.DelayFrame(APPLY_DELAY_FRAMES, cancellationToken: ct);

            Vector3 finalPosition = world.Get<CharacterTransform>(playerEntity).Position;

            var result = new JObject
            {
                ["position"] = finalPosition.ToVector(),
                ["parcel"] = finalPosition.ToParcel().ToParcel(),
            };

            if (lookAtTarget != null)
                ReportCameraAim(result, lookAtTarget.Value, cameraLookAtApplied);

            return McpToolResult.Json(result);
        }

        /// <summary>
        ///     True once the camera input system consumed the look-at intent; false when it
        ///     was still pending at the deadline (a scene-controlled camera leaves it in place).
        /// </summary>
        private async UniTask<bool> WaitForCameraLookAtAsync(CancellationToken ct)
        {
            SingleInstanceEntity camera = world.CacheCamera();
            float deadline = UnityEngine.Time.realtimeSinceStartup + CAMERA_LOOK_AT_DEADLINE_SEC;

            while (world.Has<CameraLookAtIntent>(camera))
            {
                if (UnityEngine.Time.realtimeSinceStartup >= deadline)
                    return false;

                await UniTask.Yield(ct);
            }

            return true;
        }

        private void ReportCameraAim(JObject result, Vector3 lookAtTarget, bool applied)
        {
            Vector3 cameraPosition = exposedCameraData.WorldPosition.Value;
            Quaternion cameraRotation = exposedCameraData.WorldRotation.Value;

            result["cameraRotationEuler"] = cameraRotation.eulerAngles.ToVector();
            result["aimErrorDegrees"] = Math.Round(Vector3.Angle(cameraRotation * Vector3.forward, lookAtTarget - cameraPosition), 1);

            if (!applied)
                result["warning"] = CAMERA_LOOK_AT_NOT_APPLIED;
        }
    }
}
