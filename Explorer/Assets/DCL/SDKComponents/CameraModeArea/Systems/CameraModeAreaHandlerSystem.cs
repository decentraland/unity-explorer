using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Arch.SystemGroups.Throttling;
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
        public CameraModeAreaHandlerSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SetupCameraModeAreaQuery(World);
        }

        [Query]
        [None(typeof(CameraModeAreaComponent))]
        private void SetupCameraModeArea(in Entity entity, ref TransformComponent transformComponent, ref PBCameraModeArea pbCameraModeArea) { }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
