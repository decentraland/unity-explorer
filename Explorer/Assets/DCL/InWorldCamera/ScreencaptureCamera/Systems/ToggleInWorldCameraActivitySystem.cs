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

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateBefore(typeof(ApplyCinemachineCameraInputSystem))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ToggleInWorldCameraActivitySystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly GameObject hud;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;
        private CharacterController followTarget;

        public ToggleInWorldCameraActivitySystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hud) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hud = hud;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();

            cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            cinemachinePreset.InWorldCameraData.Camera.enabled = false;

            followTarget = new GameObject("InWorldCameraFollowTarget").AddComponent<CharacterController>();
            followTarget.gameObject.layer = LayerMask.NameToLayer("CharacterController");

            followTarget.slopeLimit = 0;
            followTarget.stepOffset = 0;
            followTarget.skinWidth = 0.01f;

            followTarget.minMoveDistance = 0;
            followTarget.center = Vector3.zero;
            followTarget.radius = 0.1f;
            followTarget.height = 0.2f;

            followTarget.enabled = false;

            cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            cinemachinePreset.InWorldCameraData.Camera.enabled = false;
        }

        protected override void Update(float t)
        {
            if (World.Has<InWorldCamera>(camera) && !cinemachinePreset.Brain.IsBlending && !followTarget.enabled)
            {
                var virtualCamera = cinemachinePreset.InWorldCameraData.Camera;

                followTarget.transform.SetPositionAndRotation(virtualCamera.transform.position, virtualCamera.transform.rotation);
                virtualCamera.Follow = followTarget.transform;
                virtualCamera.LookAt = followTarget.transform;
                followTarget.enabled = true;

                var hardLock = virtualCamera.GetCinemachineComponent<CinemachineHardLockToTarget>();
                hardLock.m_Damping = 0f;
                var aim = virtualCamera.GetCinemachineComponent<CinemachineSameAsFollowTarget>();
                aim.m_Damping = 0f;

                cinemachinePreset.Brain.ManualUpdate();

                hardLock.m_Damping = 1f;
                aim.m_Damping = 1f;
            }

            if (inputSchema.ToggleInWorld!.triggered)
            {
                if (World.Has<InWorldCamera>(camera))
                    DisableCamera();
                else
                    EnableCamera();
            }
        }

        private void DisableCamera()
        {
            hud.SetActive(false); // TODO (Vit):Temporary solution, will be replaced by MVC
            World.Remove<InWorldCamera>(camera);

            cinemachinePreset.InWorldCameraData.Camera.Follow = null;
            cinemachinePreset.InWorldCameraData.Camera.LookAt = null;
            followTarget.enabled = false;

            camera.GetCameraComponent(World).Mode = CameraMode.ThirdPerson;

            SingleInstanceEntity inputMap = World.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.PLAYER);
            inputMapComponent.BlockInput(InputMapComponent.Kind.IN_WORLD_CAMERA);
        }

        private void EnableCamera()
        {
            hud.SetActive(true); // TODO (Vit):Temporary solution, will be replaced by MVC

            World.Add(camera, new InWorldCamera { FollowTarget = followTarget });

            ref CameraComponent cameraComponent = ref camera.GetCameraComponent(World);

            ref CinemachineCameraState cameraState = ref World.Get<CinemachineCameraState>(camera);
            cameraState.CurrentCamera.enabled = false;

            if(cameraComponent.Mode == CameraMode.FirstPerson)
            {
                    cinemachinePreset.InWorldCameraData.Camera.m_Transitions.m_InheritPosition = false;

                    Vector3 lookDirection = cinemachinePreset.FirstPersonCameraData.Camera.transform.forward;
                    Vector3 behindPosition = cinemachinePreset.FirstPersonCameraData.Camera.transform.position - (lookDirection * 3f) + (Vector3.up * 0.5f);

                    cinemachinePreset.InWorldCameraData.Camera.transform.position = behindPosition;
                    cinemachinePreset.InWorldCameraData.Camera.transform.rotation = cinemachinePreset.FirstPersonCameraData.Camera.transform.rotation;

                    cinemachinePreset.InWorldCameraData.Camera.m_Lens.FieldOfView = cinemachinePreset.FirstPersonCameraData.Camera.m_Lens.FieldOfView;
            }
            else
            {
                cinemachinePreset.InWorldCameraData.Camera.m_Transitions.m_InheritPosition = true;
                cinemachinePreset.InWorldCameraData.Camera.m_Lens.FieldOfView = cinemachinePreset.ThirdPersonCameraData.Camera.m_Lens.FieldOfView;
            }

            cameraComponent.Mode = CameraMode.InWorld;
            cameraState.CurrentCamera = cinemachinePreset.InWorldCameraData.Camera;
            cameraState.CurrentCamera.enabled = true;

            // Input block
            SingleInstanceEntity inputMap = World.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.IN_WORLD_CAMERA);
            inputMapComponent.BlockInput(InputMapComponent.Kind.PLAYER);
        }
    }
}
