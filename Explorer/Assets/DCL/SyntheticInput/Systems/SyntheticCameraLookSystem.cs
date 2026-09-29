using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Systems;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using ECS.Abstract;
using UnityEngine;
using Utility.Arch;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Re-asserts a held <see cref="SyntheticCameraLookIntent" /> delta into <see cref="CameraInput.Delta" /> after
    ///     <see cref="UpdateCameraInputSystem" /> has overwritten it, so a driver can turn the camera without an OS cursor lock.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateCameraInputSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticCameraLookSystem : BaseUnityLoopSystem
    {
        private const float AIM_TOLERANCE_DEGREES = 0.75f;

        // Bounded in time, not frames, so the refinement completes inside the driver-side timeout on a slow editor.
        private const float CORRECTION_BUDGET_SEC = 2.5f;

        private const int MAX_STALL_FRAMES = 10;

        private const float STALL_IMPROVEMENT_DEGREES = 0.05f;

        private const float CORRECTION_UNITS_PER_DEGREE = 1f;

        // Caps one frame's correction so a large residual pans instead of snapping.
        private const float MAX_CORRECTION_DELTA = 12f;

        private const float MIN_TARGET_DISTANCE_SQR = 0.0001f;

        private readonly Entity playerEntity;

        private SingleInstanceEntity camera;

        internal SyntheticCameraLookSystem(World world, Entity playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
        }

        public override void Initialize()
        {
            base.Initialize();
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            ref SyntheticCameraLookIntent lookIntent = ref World.TryGetRef<SyntheticCameraLookIntent>(playerEntity, out bool exists);

            if (!exists)
                return;

            if (lookIntent.LookAtTarget is { } lookAtTarget)
            {
                UpdateLookAt(ref lookIntent, lookAtTarget);
                return;
            }

            if (UnityEngine.Time.time < lookIntent.EndTime)
            {
                if (World.Has<CameraBlockerComponent>(camera))
                    return;

                ref CameraInput cameraInput = ref World.TryGetRef<CameraInput>(camera, out bool hasInput);

                if (hasInput)
                    cameraInput.Delta = lookIntent.AxisValue;
            }
            else
            {
                EcsRequest.CompleteAndRemove(World, playerEntity, lookIntent, SyntheticInputDelivery.Completed);
            }
        }

        private void UpdateLookAt(ref SyntheticCameraLookIntent lookIntent, Vector3 lookAtTarget)
        {
            if (!lookIntent.LookAtIssued)
            {
                // EndTime is stamped here too, because the camera can consume the intent within this same frame.
                lookIntent.LookAtIssued = true;
                lookIntent.LookAtBestErrorDegrees = float.MaxValue;
                lookIntent.EndTime = UnityEngine.Time.time + CORRECTION_BUDGET_SEC;

                Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;
                World.AddOrSet(camera, new CameraLookAtIntent(lookAtTarget, playerPosition));
                return;
            }

            // CameraLookAtIntent is present until the camera has applied it.
            if (World.Has<CameraLookAtIntent>(camera))
            {
                // Re-stamped while waiting, so the refinement budget starts once the camera is done.
                lookIntent.EndTime = UnityEngine.Time.time + CORRECTION_BUDGET_SEC;
                return;
            }

            RefineLookAt(ref lookIntent, lookAtTarget);
        }

        // The production look-at computes its angles from the player position, so in third person the pitch misses on
        // nearby or steep targets. The rig rate-limits input, so the error is re-measured every frame instead of corrected once.
        private void RefineLookAt(ref SyntheticCameraLookIntent lookIntent, Vector3 lookAtTarget)
        {
            CameraComponent cameraComponent = camera.GetCameraComponent(World);
            Transform cameraTransform = cameraComponent.Camera.transform;

            (float yawErrorDegrees, float pitchErrorDegrees) = AimError(cameraTransform, lookAtTarget);
            float errorDegrees = Mathf.Max(Mathf.Abs(yawErrorDegrees), Mathf.Abs(pitchErrorDegrees));

            bool onTarget = errorDegrees <= AIM_TOLERANCE_DEGREES;
            bool budgetSpent = UnityEngine.Time.time >= lookIntent.EndTime;

            if (errorDegrees < lookIntent.LookAtBestErrorDegrees - STALL_IMPROVEMENT_DEGREES)
            {
                lookIntent.LookAtBestErrorDegrees = errorDegrees;
                lookIntent.LookAtStallFrames = 0;
            }
            else
                lookIntent.LookAtStallFrames++;

            // A blocked camera cannot be corrected, so the request completes instead of waiting.
            bool blocked = World.Has<CameraBlockerComponent>(camera);

            if (onTarget || budgetSpent || blocked || lookIntent.LookAtStallFrames >= MAX_STALL_FRAMES)
            {
                EcsRequest.CompleteAndRemove(World, playerEntity, lookIntent, SyntheticInputDelivery.Completed);
                return;
            }

            ref CameraInput cameraInput = ref World.TryGetRef<CameraInput>(camera, out bool hasInput);

            if (hasInput)
                cameraInput.Delta = new Vector2(
                    Mathf.Clamp(yawErrorDegrees * CORRECTION_UNITS_PER_DEGREE, -MAX_CORRECTION_DELTA, MAX_CORRECTION_DELTA),
                    Mathf.Clamp(pitchErrorDegrees * CORRECTION_UNITS_PER_DEGREE, -MAX_CORRECTION_DELTA, MAX_CORRECTION_DELTA));
        }

        // Signed degrees in the sign convention of the look input: positive yaw turns right, positive pitch looks up.
        private static (float yawErrorDegrees, float pitchErrorDegrees) AimError(Transform cameraTransform, Vector3 lookAtTarget)
        {
            Vector3 desired = lookAtTarget - cameraTransform.position;

            if (desired.sqrMagnitude < MIN_TARGET_DISTANCE_SQR)
                return (0f, 0f);

            desired.Normalize();
            Vector3 forward = cameraTransform.forward;

            float desiredYaw = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
            float currentYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            float desiredPitch = Mathf.Asin(Mathf.Clamp(desired.y, -1f, 1f)) * Mathf.Rad2Deg;
            float currentPitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            return (Mathf.DeltaAngle(currentYaw, desiredYaw), desiredPitch - currentPitch);
        }
    }
}
