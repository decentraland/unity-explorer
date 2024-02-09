using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraModeArea.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using System;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    [ThrottlingEnabled]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly MainPlayerTransform mainPlayerTransform;

        public CameraModeAreaHandlerSystem(World world, MainPlayerTransform mainPlayerTransform) : base(world)
        {
            this.mainPlayerTransform = mainPlayerTransform;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerTransform.Configured) return;

            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(CameraModeAreaComponent))]
        private void SetupCameraModeArea(in Entity entity, ref TransformComponent transformComponent, ref PBCameraModeArea pbCameraModeArea) { }

        // [Query]
        // private void UpdateCameraModeArea(in Entity entity, ref TransformComponent transformComponent, ref PBCameraModeArea pbCameraModeArea, ref CameraModeAreaComponent cameraModeAreaComponent) { }

        private void OnEnteredArea() { }

        private void OnExitedArea() { }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
