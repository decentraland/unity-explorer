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
        private const float BEHIND_DIRECTION_OFFSET = 3f;
        private const float BEHIND_UP_OFFSET = 0.5f;
        private const float TRANSLATION_DAMPING = 1f;
        private const float AIM_DAMPING = 1f;

        private readonly Vector3 behindUpOffset = Vector3.up * BEHIND_UP_OFFSET;

        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly GameObject hud;
        private readonly CharacterController followTarget;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity inputMap;

        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera inWorldVirtualCamera;

        public ToggleInWorldCameraActivitySystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hud, CharacterController followTarget) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hud = hud;
            this.followTarget = followTarget;
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

            hardLock.m_Damping = TRANSLATION_DAMPING;
            aim.m_Damping = AIM_DAMPING;
        }

        private void DisableCamera()
        {
            hud.SetActive(false); // TODO (Vit):Temporary solution, will be replaced by MVC

            World.Remove<InWorldCamera>(camera);

            SwitchToThirdPersonCamera();
            SwitchCameraInput(to: Kind.PLAYER);
        }

        private void EnableCamera()
        {
            hud.SetActive(true); // TODO (Vit):Temporary solution, will be replaced by MVC

            World.Add(camera, new InWorldCamera { FollowTarget = followTarget });

            SwitchToInWorldCamera();
            SwitchCameraInput(to: Kind.IN_WORLD_CAMERA);
        }

        private void SwitchToThirdPersonCamera()
        {
            inWorldVirtualCamera.Follow = null;
            inWorldVirtualCamera.LookAt = null;
            followTarget.enabled = false;
            camera.GetCameraComponent(World).Mode = CameraMode.ThirdPerson;
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
                inWorldVirtualCamera.m_Lens.FieldOfView = cinemachinePreset.FirstPersonCameraData.Camera.m_Lens.FieldOfView;
            }
            else
            {
                inWorldVirtualCamera.m_Transitions.m_InheritPosition = true;
                inWorldVirtualCamera.m_Lens.FieldOfView = cinemachinePreset.ThirdPersonCameraData.Camera.m_Lens.FieldOfView;
            }
        }

        private Vector3 CalculateBehindPosition()
        {
            Vector3 lookDirection = cinemachinePreset.FirstPersonCameraData.Camera.transform.forward;
            return cinemachinePreset.FirstPersonCameraData.Camera.transform.position - (lookDirection * BEHIND_DIRECTION_OFFSET) + behindUpOffset;
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
