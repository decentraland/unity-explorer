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
                // Written through the ref before the AddOrSet below, which is a structural change.
                lookIntent.LookAtIssued = true;

                Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;
                World.AddOrSet(camera, new CameraLookAtIntent(lookAtTarget, playerPosition));
                return;
            }

            // ApplyCinemachineCameraInputSystem removes the camera intent once it applied the rotation.
            if (!World.Has<CameraLookAtIntent>(camera))
                EcsRequest.CompleteAndRemove(World, playerEntity, lookIntent, SyntheticInputDelivery.Completed);
        }
    }
}
