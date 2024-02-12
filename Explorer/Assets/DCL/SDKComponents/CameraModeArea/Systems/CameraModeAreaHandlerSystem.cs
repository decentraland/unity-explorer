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
        private readonly MainPlayerTransform mainPlayerTransform; // TODO: We may be able to get rid of this if we use Unity collision events...

        public CameraModeAreaHandlerSystem(World world, MainPlayerTransform mainPlayerTransform) : base(world)
        {
            this.mainPlayerTransform = mainPlayerTransform;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerTransform.Configured) return;

            // TODO: Check if we have control of the camera mode as well

            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(CameraModeAreaComponent))]
        private void SetupCameraModeArea(in Entity entity, ref TransformComponent transformComponent, ref PBCameraModeArea pbCameraModeArea)
        {
            // TODO: Instantiate MainPlayerTriggerArea from pool
            // TODO: subscribe to MainPlayerTriggerArea events
        }

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
