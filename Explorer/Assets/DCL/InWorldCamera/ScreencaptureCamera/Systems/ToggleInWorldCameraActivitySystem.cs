using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.InWorldCamera.ScreencaptureCamera.Settings;
using ECS.Abstract;
using UnityEngine;
using static DCL.Input.Component.InputMapComponent;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateBefore(typeof(ApplyCinemachineCameraInputSystem))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ToggleInWorldCameraActivitySystem : BaseUnityLoopSystem
    {
        private readonly Vector3 behindUpOffset;

        private readonly InWorldCameraTransitionSettings settings;
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly GameObject hud;
        private readonly CharacterController followTarget;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity inputMap;

        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera inWorldVirtualCamera;

        public ToggleInWorldCameraActivitySystem(World world, InWorldCameraTransitionSettings settings, DCLInput.InWorldCameraActions inputSchema, GameObject hud, CharacterController followTarget) : base(world)
        {
            this.settings = settings;
            this.inputSchema = inputSchema;
            this.hud = hud;
            this.followTarget = followTarget;

            behindUpOffset = Vector3.up * settings.BehindUpOffset;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            inputMap = World.CacheInputMap();

            cinemachinePreset = World.Get<ICinemachinePreset>(camera);

            inWorldVirtualCamera = cinemachinePreset.InWorldCameraData.Camera;
            inWorldVirtualCamera.enabled = false;
        }

        protected override void Update(float t)
        {
            if (World.Has<InWorldCamera>(camera) && BlendingHasFinished())
                SetFollowTarget();

            if (inputSchema.ToggleInWorld!.triggered)
            {
                if (World.Has<InWorldCamera>(camera))
                    DisableCamera();
                else
                    EnableCamera();
            }

            bool BlendingHasFinished() =>
                !followTarget.enabled && !cinemachinePreset.Brain.IsBlending;
        }

        private void SetFollowTarget()
        {
            followTarget.transform.SetPositionAndRotation(inWorldVirtualCamera.transform.position, inWorldVirtualCamera.transform.rotation);
            inWorldVirtualCamera.Follow = followTarget.transform;
            inWorldVirtualCamera.LookAt = followTarget.transform;
            followTarget.enabled = true;

            CinemachineHardLockToTarget? hardLock = inWorldVirtualCamera.GetCinemachineComponent<CinemachineHardLockToTarget>();
            CinemachineSameAsFollowTarget? aim = inWorldVirtualCamera.GetCinemachineComponent<CinemachineSameAsFollowTarget>();

            hardLock.m_Damping = 0f;
            aim.m_Damping = 0f;

            cinemachinePreset.Brain.ManualUpdate();

            hardLock.m_Damping = settings.TranslationDamping;
            aim.m_Damping = settings.AimDamping;
        }

        private void DisableCamera()
        {
            hud.SetActive(false); // TODO (Vit):Temporary solution, will be replaced by MVC

            SwitchToThirdPersonCamera();
            SwitchCameraInput(to: Kind.PLAYER);

            World.Remove<InWorldCamera, CameraTarget, CameraDampedFOV, CameraDampedAim, InWorldCameraInput>(camera);
        }

        private void EnableCamera()
        {
            hud.SetActive(true); // TODO (Vit):Temporary solution, will be replaced by MVC

            SwitchToInWorldCamera();
            SwitchCameraInput(to: Kind.IN_WORLD_CAMERA);

            World.Add(camera,
                new InWorldCamera(),
                new CameraTarget { Value = followTarget },
                new CameraDampedFOV { Current = inWorldVirtualCamera.m_Lens.FieldOfView, Velocity = 0f, Target = inWorldVirtualCamera.m_Lens.FieldOfView },
                new CameraDampedAim { Current = Vector2.up, Velocity = Vector2.up },
                new InWorldCameraInput());
        }

        private void SwitchToThirdPersonCamera()
        {
            inWorldVirtualCamera.Follow = null;
            inWorldVirtualCamera.LookAt = null;
            followTarget.enabled = false;

            float distanceToThirdPersonView =
                Mathf.Abs(cinemachinePreset.ThirdPersonCameraData.Camera.transform.localPosition.z - inWorldVirtualCamera.transform.localPosition.z);
            float distanceToDroneCameraView =
                Mathf.Abs(cinemachinePreset.DroneViewCameraData.Camera.transform.localPosition.z - inWorldVirtualCamera.transform.localPosition.z);

            camera.GetCameraComponent(World).Mode = distanceToDroneCameraView < distanceToThirdPersonView ? CameraMode.DroneView : CameraMode.ThirdPerson;
        }

        private void SwitchToInWorldCamera()
        {
            ref CameraComponent cameraComponent = ref camera.GetCameraComponent(World);

            ref CinemachineCameraState cameraState = ref World.Get<CinemachineCameraState>(camera);
            cameraState.CurrentCamera.enabled = false;

            SetCameraTransition(cameraComponent.Mode);

            cameraState.CurrentCamera = inWorldVirtualCamera;
            cameraState.CurrentCamera.enabled = true;
            cameraComponent.Mode = CameraMode.InWorld;
        }

        private void SetCameraTransition(CameraMode currentCameraMode)
        {
            if (currentCameraMode == CameraMode.FirstPerson)
            {
                inWorldVirtualCamera.m_Transitions.m_InheritPosition = false;
                inWorldVirtualCamera.transform.position = CalculateBehindPosition();
                inWorldVirtualCamera.transform.rotation = cinemachinePreset.FirstPersonCameraData.Camera.transform.rotation;
            }
            else
                inWorldVirtualCamera.m_Transitions.m_InheritPosition = true;

            inWorldVirtualCamera.m_Lens.FieldOfView = CurrentFov(currentCameraMode);
        }

        private float CurrentFov(CameraMode currentCameraMode) =>
            currentCameraMode switch
            {
                CameraMode.FirstPerson => cinemachinePreset.FirstPersonCameraData.Camera.m_Lens.FieldOfView,
                CameraMode.ThirdPerson => cinemachinePreset.ThirdPersonCameraData.Camera.m_Lens.FieldOfView,
                CameraMode.DroneView => cinemachinePreset.DroneViewCameraData.Camera.m_Lens.FieldOfView,
                _ => 60f,
            };

        private Vector3 CalculateBehindPosition()
        {
            Vector3 lookDirection = cinemachinePreset.FirstPersonCameraData.Camera.transform.forward;
            return cinemachinePreset.FirstPersonCameraData.Camera.transform.position - (lookDirection * settings.BehindDirectionOffset) + behindUpOffset;
        }

        private void SwitchCameraInput(Kind to)
        {
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);

            switch (to)
            {
                case Kind.IN_WORLD_CAMERA:
                    inputMapComponent.UnblockInput(Kind.IN_WORLD_CAMERA);
                    inputMapComponent.BlockInput(Kind.PLAYER);
                    break;
                case Kind.PLAYER:
                    inputMapComponent.UnblockInput(Kind.PLAYER);
                    inputMapComponent.BlockInput(Kind.IN_WORLD_CAMERA);
                    break;
            }
        }
    }
}
