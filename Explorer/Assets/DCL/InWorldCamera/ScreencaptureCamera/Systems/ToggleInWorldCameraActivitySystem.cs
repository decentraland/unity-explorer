using Arch.Core;
using Arch.SystemGroups;
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
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ToggleInWorldCameraActivitySystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly GameObject hud;

        private SingleInstanceEntity camera;

        public ToggleInWorldCameraActivitySystem(World world, DCLInput.InWorldCameraActions inputSchema, GameObject hud) : base(world)
        {
            this.inputSchema = inputSchema;
            this.hud = hud;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();

            var cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            cinemachinePreset.InWorldCameraData.Camera.enabled = false;
        }

        protected override void Update(float t)
        {
            if (inputSchema.ToggleInWorld!.triggered)
            {
                if (World.Has<IsInWorldCamera>(camera))
                    DisableCamera();
                else
                    EnableCamera();
            }
        }

        private void EnableCamera()
        {
            hud.SetActive(true); // TODO (Vit):Temporary solution, will be replaced by MVC
            World.Add<IsInWorldCamera>(camera);
            ref var cameraComponent = ref camera.GetCameraComponent(World);
            cameraComponent.Mode = CameraMode.InWorld;

            ref var cameraState = ref World.Get<CinemachineCameraState>(camera);
            var cinemachinePreset = World.Get<ICinemachinePreset>(camera);

            cameraState.CurrentCamera.enabled = false;
            cameraState.CurrentCamera = cinemachinePreset.InWorldCameraData.Camera;
            cameraState.CurrentCamera.enabled = true;
            cinemachinePreset.InWorldCameraData.Camera.transform.position = cinemachinePreset.ThirdPersonCameraData.Camera.transform.position;

            var  inputMap = World.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.IN_WORLD_CAMERA);
            inputMapComponent.BlockInput(InputMapComponent.Kind.PLAYER);

            // copy POV
            // preset.InWorldCameraData.POV.m_HorizontalAxis.Value = preset.ThirdPersonCameraData.Camera.m_XAxis.Value;
            // preset.InWorldCameraData.POV.m_VerticalAxis.Value = preset.ThirdPersonCameraData.Camera.m_YAxis.Value;
        }

        private void DisableCamera()
        {
            hud.SetActive(false); // TODO (Vit):Temporary solution, will be replaced by MVC
            World.Remove<IsInWorldCamera>(camera);
            camera.GetCameraComponent(World).Mode = CameraMode.ThirdPerson;

            var  inputMap = World.CacheInputMap();
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
            inputMapComponent.UnblockInput(InputMapComponent.Kind.PLAYER);
            inputMapComponent.BlockInput(InputMapComponent.Kind.IN_WORLD_CAMERA);
        }
    }
}
