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
    ///     Delivers automation-driver camera-look requests while a <see cref="SyntheticCameraLookIntent" /> is
    ///     present on the player entity. A held delta is re-asserted into <see cref="CameraInput.Delta" /> after
    ///     <see cref="UpdateCameraInputSystem" /> wrote (or zeroed) it, so the Cinemachine axes consume it exactly
    ///     like mouse-look; unlike real look input it does not require an OS cursor lock, as a driver has no
    ///     cursor to lock. A look-at is translated into the production <see cref="CameraLookAtIntent" />.
    ///     A <see cref="CameraBlockerComponent" /> suppresses the held delta the same way it suppresses real
    ///     camera input; the hold keeps running until its expiry.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [UpdateAfter(typeof(UpdateCameraInputSystem))]
    [LogCategory(ReportCategory.SYNTHETIC_INPUT)]
    public partial class SyntheticCameraLookSystem : BaseUnityLoopSystem
    {
        /// <summary>Aim error at which a look-at is considered on target — under half a reticle's worth.</summary>
        private const float AIM_TOLERANCE_DEGREES = 0.75f;

        /// <summary>
        ///     Seconds the aim refinement may spend before the request completes with whatever it achieved. Bounded
        ///     in time, not frames, so it stays inside the driver-side completion grace on a slow editor too.
        /// </summary>
        private const float CORRECTION_BUDGET_SEC = 2.5f;

        /// <summary>Consecutive frames without improvement after which the rig is treated as unable to get closer.</summary>
        private const int MAX_STALL_FRAMES = 10;

        /// <summary>Improvement below this is noise, not progress.</summary>
        private const float STALL_IMPROVEMENT_DEGREES = 0.05f;

        /// <summary>Look-input units requested per degree of remaining error; the rig rate-limits the rest.</summary>
        private const float CORRECTION_UNITS_PER_DEGREE = 1f;

        /// <summary>Cap on one frame's correction, so a large residual pans instead of snapping.</summary>
        private const float MAX_CORRECTION_DELTA = 12f;

        /// <summary>A target on top of the camera has no meaningful direction to aim at.</summary>
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
                // The intent is copied out before the structural removal; no component refs are touched afterwards.
                EcsRequest.CompleteAndRemove(World, playerEntity, lookIntent, SyntheticInputDelivery.Completed);
            }
        }

        private void UpdateLookAt(ref SyntheticCameraLookIntent lookIntent, Vector3 lookAtTarget)
        {
            if (!lookIntent.LookAtIssued)
            {
                // Written through the ref before the AddOrSet below, which is a structural change. The budget is
                // stamped here as well: the camera can consume the intent within this same frame, and the
                // refinement must not find an expired budget when it does.
                lookIntent.LookAtIssued = true;
                lookIntent.LookAtBestErrorDegrees = float.MaxValue;
                lookIntent.EndTime = UnityEngine.Time.time + CORRECTION_BUDGET_SEC;

                Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;
                World.AddOrSet(camera, new CameraLookAtIntent(lookAtTarget, playerPosition));
                return;
            }

            // ApplyCinemachineCameraInputSystem removes the camera intent once it applied the rotation.
            if (World.Has<CameraLookAtIntent>(camera))
            {
                // Restamped every frame it waits, so the refinement budget starts when the camera is actually done.
                lookIntent.EndTime = UnityEngine.Time.time + CORRECTION_BUDGET_SEC;
                return;
            }

            RefineLookAt(ref lookIntent, lookAtTarget);
        }

        /// <summary>
        ///     <para>
        ///         Brings the camera the rest of the way onto the target. The production look-at drives the rig's
        ///         orbit value, which is only an approximation of a pitch: it computes the angle from the player's
        ///         feet and maps it onto the orbit range, so a third-person camera — which sits behind and above
        ///         the player — ends up with the right yaw but an aim that misses vertically, by tens of degrees on
        ///         nearby or steep targets. A driver asking to look at a point means the point ends up under the
        ///         reticle, so the residual is closed here through the same look-input channel mouse-look uses.
        ///     </para>
        ///     <para>
        ///         Rate limiting inside the rig makes an open-loop correction impossible (a large delta saturates
        ///         at the axis' max speed), so the error is re-measured every frame and the delta shrinks with it.
        ///         The loop always terminates: on target, on a frame budget, or as soon as the error stops
        ///         improving — which is what a clamped rig (third-person pitch limits) looks like from here.
        ///     </para>
        /// </summary>
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

            // A blocked camera cannot be corrected at all; completing beats holding the request open.
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

        /// <summary>
        ///     Signed yaw/pitch error in degrees between the camera's forward and the direction to the target from
        ///     the camera itself, in the sign convention of the look input: positive yaw turns right, positive
        ///     pitch looks up.
        /// </summary>
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
