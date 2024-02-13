using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.MainPlayerTriggerArea;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(MainPlayerTriggerAreaHandlerSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    [ThrottlingEnabled]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly MainPlayerTransform mainPlayerTransform; // TODO: We may be able to get rid of this if we use Unity collision events...

        public CameraModeAreaHandlerSystem(World world, MainPlayerTransform mainPlayerTransform) : base(world)
        {
            this.mainPlayerTransform = mainPlayerTransform;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerTransform.Configured) return;

            // TODO: Check if we have control of the camera mode as well

            UpdateCameraModeAreaQuery(World);
            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(MainPlayerTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            CameraType targetCameraMode = pbCameraModeArea.Mode;

            World.Add(entity, new MainPlayerTriggerAreaComponent
            {
                areaSize = pbCameraModeArea.Area,
                OnEnteredTrigger = () =>
                {
                    // change camera mode towards pbCameraModeArea.Mode
                    // lock camera mode
                    Debug.Log($"PRAVS - CHANGE CAMERA MODE TO {targetCameraMode}");
                },
                OnExitedTrigger = () => { Debug.Log("RESET CAMERA MODE"); },
                IsDirty = true,
            });
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateCameraModeArea(ref PBCameraModeArea pbCameraModeArea, ref MainPlayerTriggerAreaComponent mainPlayerTriggerAreaComponent)
        {
            if (!pbCameraModeArea.IsDirty) return;

            Debug.Log($"PRAVS - Update CAMERA MODE AREA SIZE from {mainPlayerTriggerAreaComponent.areaSize} to {pbCameraModeArea.Area}");

            // TODO: Support changing actions as well?
            mainPlayerTriggerAreaComponent.areaSize = pbCameraModeArea.Area;
            mainPlayerTriggerAreaComponent.IsDirty = true;
        }

        private void OnEnteredArea() { }

        private void OnExitedArea() { }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
