using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class EmitInWorldCameraInputSystem : BaseUnityLoopSystem
    {
        private const string SOURCE_SHORTCUT = "Shortcut";

        private readonly DCLInput.InWorldCameraActions inputSchema;

        private SingleInstanceEntity camera;

        private EmitInWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions inputSchema) : base(world)
        {
            this.inputSchema = inputSchema;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!World.Get<CameraComponent>(camera).CameraInputChangeEnabled) return;

            if (inputSchema.CameraReel.triggered || inputSchema.Close.triggered)
                World.Add(camera, new ToggleInWorldCameraRequest { IsEnable = false });

            ref InWorldCameraInput input = ref World.TryGetRef<InWorldCameraInput>(camera, out bool exists);

            if (exists)
            {
                input.Translation = inputSchema.Translation.ReadValue<Vector2>();
                input.Panning = inputSchema.Panning.ReadValue<float>();
                input.Tilting = inputSchema.Tilting.ReadValue<float>();
                input.IsRunning = inputSchema.Run.IsPressed();

                input.Aim = inputSchema.Rotation.ReadValue<Vector2>();
                input.MouseIsDragging = inputSchema.MouseDrag.IsPressed();
                input.Zoom = inputSchema.Zoom.ReadValue<float>();

                if (inputSchema.Screenshot.triggered && !World.Has<TakeScreenshotRequest>(camera))
                    World.Add(camera, new TakeScreenshotRequest { Source = SOURCE_SHORTCUT});
            }
        }
    }
}
