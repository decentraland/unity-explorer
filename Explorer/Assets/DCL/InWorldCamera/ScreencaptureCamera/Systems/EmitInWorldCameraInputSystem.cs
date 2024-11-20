using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class EmitInWorldCameraInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private SingleInstanceEntity camera;

        public EmitInWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions inputSchema) : base(world)
        {
            this.inputSchema = inputSchema;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            ref InWorldCameraInput input = ref World.TryGetRef<InWorldCameraInput>(camera, out bool exists);

            if (exists)
            {
                input.Translation = inputSchema.Translation.ReadValue<Vector2>();
                input.Panning = inputSchema.Panning.ReadValue<float>();
                input.IsRunning = inputSchema.Run.IsPressed();

                input.Aim = inputSchema.Rotation.ReadValue<Vector2>();
                input.MouseIsDragging = inputSchema.MouseDrag.IsPressed();
                input.Zoom = inputSchema.Zoom.ReadValue<float>();
            }
        }
    }
}
