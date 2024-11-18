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

            followTarget.slopeLimit = 0;
            followTarget.stepOffset = 0;
            followTarget.skinWidth = 0.01f;

            followTarget.minMoveDistance = 0;
            followTarget.center = Vector3.zero;
            followTarget.radius = 0.1f;
            followTarget.height = 0.2f;

            followTarget.enabled = false;
        }

        protected override void Update(float t)
        {
            if (inputSchema.ToggleInWorld!.triggered)
            {
                if (World.Has<InWorldCamera>(camera))
                    DisableCamera();
                else
                    EnableCamera();
            }
        }

        private void EnableCamera()
        {
            hud.SetActive(true); // TODO (Vit):Temporary solution, will be replaced by MVC
            World.Add(camera, new InWorldCamera { FollowTarget = followTarget });

            ref CameraComponent cameraComponent = ref camera.GetCameraComponent(World);
            cameraComponent.Mode = CameraMode.InWorld;

            ref CinemachineCameraState cameraState = ref World.Get<CinemachineCameraState>(camera);

            cameraState.CurrentCamera.enabled = false;
            cameraState.CurrentCamera = cinemachinePreset.InWorldCameraData.Camera;
            cameraState.CurrentCamera.enabled = true;

            cameraState.CurrentCamera.Follow = followTarget.transform;
            cameraState.CurrentCamera.LookAt = followTarget.transform;

            followTarget.transform.position = cinemachinePreset.ThirdPersonCameraData.Camera.transform.position;
            followTarget.transform.rotation = cinemachinePreset.ThirdPersonCameraData.Camera.transform.rotation;
            followTarget.enabled = true;

            // copy Position and POV
            cinemachinePreset.InWorldCameraData.Camera.transform.position = cinemachinePreset.ThirdPersonCameraData.Camera.transform.position;
            cinemachinePreset.InWorldCameraData.Camera.transform.rotation = cinemachinePreset.ThirdPersonCameraData.Camera.transform.rotation;

            // Input block
            SingleInstanceEntity inputMap = World.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.IN_WORLD_CAMERA);
            inputMapComponent.BlockInput(InputMapComponent.Kind.PLAYER);
        }

        private void DisableCamera()
        {
            hud.SetActive(false); // TODO (Vit):Temporary solution, will be replaced by MVC
            World.Remove<InWorldCamera>(camera);
            camera.GetCameraComponent(World).Mode = CameraMode.ThirdPerson;

            SingleInstanceEntity inputMap = World.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.PLAYER);
            inputMapComponent.BlockInput(InputMapComponent.Kind.IN_WORLD_CAMERA);
        }
    }
}
