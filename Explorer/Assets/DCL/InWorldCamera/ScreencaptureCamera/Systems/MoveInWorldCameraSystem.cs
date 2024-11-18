using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class MoveInWorldCameraSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;

        public MoveInWorldCameraSystem(World world, DCLInput.InWorldCameraActions inputSchema) : base(world)
        {
            this.inputSchema = inputSchema;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();

            cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            cinemachinePreset.InWorldCameraData.Camera.enabled = false;
        }


        protected override void Update(float t)
        {
            if(World.Has<InWorldCamera>(camera))
            {
                ref var cameraInput = ref World.Get<CameraInput>(camera);
                cameraInput.FreeMovement = inputSchema.Translation.ReadValue<Vector2>();
                // cameraInput.FreePanning = freeCameraActions.Panning.ReadValue<Vector2>();
                // cameraInput.FreeFOV = freeCameraActions.FOV.ReadValue<Vector2>();

                ApplyInWorldCameraMovement(t, in camera.GetCameraComponent(World), in cameraInput, cinemachinePreset);
                cinemachinePreset.Brain.ManualUpdate(); // Update the brain manually
            }
        }

        private static void ApplyInWorldCameraMovement(float dt, in CameraComponent camera, in CameraInput cameraInput,
            ICinemachinePreset cinemachinePreset)
        {
            // Camera's position is under Cinemachine control
            Transform cinemachineTransform = cinemachinePreset.InWorldCameraData.Camera.transform;

            // Camera's rotation is not
            Transform cameraTransform = camera.Camera.transform;
            Vector3 direction = (cameraTransform.forward * cameraInput.FreeMovement.y) +
                                (cameraTransform.up * cameraInput.FreePanning.y) +
                                (cameraTransform.right * cameraInput.FreeMovement.x);

            cinemachineTransform.localPosition += direction * (cinemachinePreset.InWorldCameraData.Speed * dt);
        }

        // private static void ApplyInWorldFOV(float dt, ICinemachinePreset cinemachinePreset, in CameraInput cameraInput)
        // {
        //     CinemachineVirtualCamera tpc = cinemachinePreset.InWorldCameraData.Camera;
        //     LensSettings tpcMLens = tpc.m_Lens;
        //     tpcMLens.FieldOfView += cameraInput.FreeFOV.y * cinemachinePreset.InWorldCameraData.Speed * dt;
        //     tpc.m_Lens = tpcMLens;
        // }
        //
        // private static void ApplyPOV(CinemachinePOV cinemachinePOV, in CameraInput cameraInput)
        // {
        //     if (cinemachinePOV)
        //     {
        //         cinemachinePOV.m_HorizontalAxis.m_InputAxisValue = cameraInput.Delta.x;
        //         cinemachinePOV.m_VerticalAxis.m_InputAxisValue = cameraInput.Delta.y;
        //     }
        // }
    }
}
