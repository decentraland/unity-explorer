﻿using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class EmitInWorldCameraInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly InputAction toggleInWorldCameraShortcut;

        private SingleInstanceEntity camera;

        public EmitInWorldCameraInputSystem(World world, DCLInput.InWorldCameraActions inputSchema, InputAction toggleInWorldCameraShortcut) : base(world)
        {
            this.inputSchema = inputSchema;
            this.toggleInWorldCameraShortcut = toggleInWorldCameraShortcut;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (toggleInWorldCameraShortcut.triggered)
                World.Add(camera, new ToggleInWorldCameraRequest { IsEnable = !World.Has<InWorldCameraComponent>(camera) });

            ref InWorldCameraInput input = ref World.TryGetRef<InWorldCameraInput>(camera, out bool exists);

            if (exists)
            {
                input.Translation = inputSchema.Translation.ReadValue<Vector2>();
                input.Panning = inputSchema.Panning.ReadValue<float>();
                input.IsRunning = inputSchema.Run.IsPressed();

                input.Aim = inputSchema.Rotation.ReadValue<Vector2>();
                input.MouseIsDragging = inputSchema.MouseDrag.IsPressed();
                input.Zoom = inputSchema.Zoom.ReadValue<float>();

                if (inputSchema.Screenshot.triggered && !World.Has<TakeScreenshotRequest>(camera))
                    World.Add(camera, new TakeScreenshotRequest { Source = "Shortcut" });
            }
        }
    }
}