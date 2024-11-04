using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class InWorldCameraInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions cameraInput;

        public InWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions cameraInput) : base(world)
        {
            this.cameraInput = cameraInput;
        }

        protected override void Update(float t)
        {
            if (cameraInput.ToggleActivity.triggered)
            {
                Debug.Log("VVV Triggered");
            }

            EmitInputQuery(World);
        }

        [Query]
        [All(typeof(CameraComponent))]
        private void EmitInput(in Entity entity) { }
    }
}
