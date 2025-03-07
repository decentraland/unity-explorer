﻿using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Components;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using DCL.InWorldCamera.Settings;
using DCL.InWorldCamera.UI;
using DCL.Nametags;
using ECS.Abstract;
using MVC;
using UnityEngine;
using UnityEngine.UIElements;
using static DCL.Input.Component.InputMapComponent;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateBefore(typeof(ApplyCinemachineCameraInputSystem))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class ToggleInWorldCameraActivitySystem : BaseUnityLoopSystem
    {
        private readonly Vector3 behindUpOffset;

        private readonly InWorldCameraTransitionSettings settings;
        private readonly InWorldCameraController hudController;
        private readonly GameObject hud;
        private readonly CharacterController followTarget;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ICursor cursor;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly UIDocument sceneUIRoot;
        private readonly NametagsData nametagsData;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity inputMap;

        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera inWorldVirtualCamera;
        private bool wasDebugVisible;

        public ToggleInWorldCameraActivitySystem(
            World world,
            InWorldCameraTransitionSettings settings,
            InWorldCameraController hudController,
            CharacterController followTarget,
            IDebugContainerBuilder debugContainerBuilder,
            ICursor cursor,
            IMVCManager mvcManager,
            DCLInput.InWorldCameraActions inputSchema,
            UIDocument sceneUIRoot,
            NametagsData nametagsData) : base(world)
        {
            this.settings = settings;
            this.hudController = hudController;
            this.followTarget = followTarget;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cursor = cursor;
            this.mvcManager = mvcManager;
            this.inputSchema = inputSchema;
            this.sceneUIRoot = sceneUIRoot;
            this.nametagsData = nametagsData;

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
            if (World.Has<InWorldCameraComponent>(camera) && !cinemachinePreset.Brain.IsBlending)
            {
                if (!followTarget.enabled)
                    SetFollowTarget();

                if (inputSchema.ShowHide.triggered)
                    hudController.ToggleVisibility();

                if(inputSchema.ToggleNametags.triggered)
                    nametagsData.showNameTags = !nametagsData.showNameTags;
            }

            if (World.TryGet(camera, out ToggleInWorldCameraRequest request))
                ToggleCamera(request.IsEnable);
        }

        private void ToggleCamera(bool enable)
        {
            if (enable)
                EnableCamera();
            else
                DisableCamera();
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
            if (debugContainerBuilder?.Container != null)
                debugContainerBuilder.IsVisible = wasDebugVisible;

            hudController.Hide();
            World.Add(camera, new ToggleUIRequest()
            {
                Enable = true,
                Except = hudController
            });

            SwitchToThirdPersonCamera();

            cursor.Unlock();
            ref CursorComponent cursorComponent = ref World.Get<CursorComponent>(camera);
            cursorComponent.CursorState = CursorState.Free;

            SwitchCameraInput(to: Kind.PLAYER);

            World.Remove<InWorldCameraComponent, CameraTarget, CameraDampedFOV, CameraDampedTilt, CameraDampedAim, InWorldCameraInput>(camera);
        }

        private void EnableCamera()
        {
            if (debugContainerBuilder?.Container != null)
            {
                wasDebugVisible = debugContainerBuilder.IsVisible;
                debugContainerBuilder.IsVisible = false;
            }

            hudController.Show();

            World.Add(camera, new ToggleUIRequest
            {
                Enable = false,
                Except = hudController
            });

            SwitchToInWorldCamera();

            cursor.Lock();
            ref CursorComponent cursorComponent = ref World.Get<CursorComponent>(camera);
            cursorComponent.CursorState = CursorState.Locked;

            SwitchCameraInput(to: Kind.IN_WORLD_CAMERA);

            World.Add(camera,
                new InWorldCameraComponent(),
                new CameraTarget { Value = followTarget },
                new CameraDampedFOV { Current = inWorldVirtualCamera.m_Lens.FieldOfView, Velocity = 0f, Target = inWorldVirtualCamera.m_Lens.FieldOfView },
                new CameraDampedTilt { Current = 0f, Target = 0f, Velocity = 0f },
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
                    inputMapComponent.BlockInput(Kind.PLAYER);
                    inputMapComponent.BlockInput(Kind.SHORTCUTS);
                    break;
                case Kind.PLAYER:
                    inputMapComponent.UnblockInput(Kind.PLAYER);
                    inputMapComponent.UnblockInput(Kind.SHORTCUTS);
                    break;
            }
        }
    }
}
