using Arch.Core;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SyntheticInput;
using DCL.SyntheticInput.Components;
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

        /// <summary>A scene-controlled camera never consumes the look-at intent, so the wait for it needs a deadline.</summary>
        private const float CAMERA_LOOK_AT_DEADLINE_SEC = 1f;

        private const string CAMERA_LOOK_AT_NOT_APPLIED =
            "the camera did not apply the look-at (a scene controls the camera, e.g. an SDK virtual camera); the player was moved — use look_at once the scene releases the camera";

        private const string AIM_REFINEMENT_NOT_RUN =
            "the aim refinement did not run (is the simulation paused?)";

        private readonly IGlobalWorldActions globalWorldActions;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly ExposedCameraData exposedCameraData;
        private readonly SyntheticInputAgent syntheticInput;

        public override string Name => "move_to";

        public override string Description =>
            "Teleport the player to a world-space position (x,y,z in meters; one parcel is 16x16m): the transform is set directly "
            + "(or lerped over durationSec), bypassing locomotion and physics — no footsteps, velocity or collision — so a scene that "
            + "watches movement, trigger areas or speed must be driven with walk instead. lookAtX/Y/Z turn the avatar toward that point "
            + "and then refine the camera onto it exactly as look_at does; the result reports aimErrorDegrees (normally ~0; a warning "
            + "explains a rig clamp or a scene-controlled camera). For crossing to another scene prefer the teleport tool.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Number("x", isRequired: true)
                  .Number("y", isRequired: true)
                  .Number("z", isRequired: true)
                  .Number("lookAtX", "World point to aim the avatar and the camera at on arrival (the camera is refined like look_at); pass all three lookAt coordinates or none.")
                  .Number("lookAtY")
                  .Number("lookAtZ")
                  .Number("durationSec", "Seconds to move over; 0 (default) teleports instantly.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public MoveToTool(IGlobalWorldActions globalWorldActions, World world, Entity playerEntity, ExposedCameraData exposedCameraData, SyntheticInputAgent syntheticInput)
        {
            this.globalWorldActions = globalWorldActions;
            this.world = world;
            this.playerEntity = playerEntity;
            this.exposedCameraData = exposedCameraData;
            this.syntheticInput = syntheticInput;
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

            // As in the SDK's movePlayerTo: MoveAndRotatePlayerAsync only moves and turns the avatar, so the camera
            // look-at is posted first as its own intent on the camera entity.
            if (lookAtTarget != null)
                globalWorldActions.RotateCamera(lookAtTarget, targetPosition);

            try
            {
                await globalWorldActions.MoveAndRotatePlayerAsync(targetPosition, lookAtTarget, lookAtTarget, durationSec, ct)
                                        .Timeout(TimeSpan.FromSeconds(durationSec + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException) { return McpToolResult.Error($"move_to did not complete within {durationSec + COMPLETION_GRACE_SEC}s."); }

            bool cameraLookAtApplied = lookAtTarget == null || await WaitForCameraLookAtAsync(ct);

            // The production look-at computes its angles from the avatar root, so it lands tens of degrees off;
            // the closed-loop refinement look_at uses puts the point under the reticle.
            SyntheticInputDelivery refinement = lookAtTarget == null
                ? SyntheticInputDelivery.Completed
                : await syntheticInput.LookAtAsync(lookAtTarget.Value, ct);

            await UniTask.DelayFrame(APPLY_DELAY_FRAMES, cancellationToken: ct);

            Vector3 finalPosition = world.Get<CharacterTransform>(playerEntity).Position;

            var result = new JObject
            {
                ["position"] = finalPosition.ToVector(),
                ["parcel"] = finalPosition.ToParcel().ToParcelJson(),
            };

            if (lookAtTarget != null)
                await ReportCameraAimAsync(result, lookAtTarget.Value, cameraLookAtApplied, refinement, ct);

            return McpToolResult.Json(result);
        }

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

        private async UniTask ReportCameraAimAsync(JObject result, Vector3 lookAtTarget, bool cameraLookAtApplied, SyntheticInputDelivery refinement, CancellationToken ct)
        {
            (Vector3 cameraPosition, Quaternion cameraRotation) = await exposedCameraData.ReadSettledPoseAsync(ct);

            CameraAimReport.Append(result, cameraPosition, cameraRotation, lookAtTarget);

            if (!cameraLookAtApplied)
                AppendWarning(result, CAMERA_LOOK_AT_NOT_APPLIED);

            if (refinement == SyntheticInputDelivery.TimedOut)
                AppendWarning(result, AIM_REFINEMENT_NOT_RUN);
        }

        private static void AppendWarning(JObject result, string warning)
        {
            string? existing = result["warning"]?.Value<string>();
            result["warning"] = string.IsNullOrEmpty(existing) ? warning : $"{existing}; {warning}";
        }
    }
}
