using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Systems.UITransform;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UITransformUpdateSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIFixPbPointerEventsSystem : BaseUnityLoopSystem
    {
        public UIFixPbPointerEventsSystem(World world) : base(world)
        {
        }

        protected override void Update(float _)
        {
            FixPointerEventsQuery(World);
        }

        [Query]
        private void FixPointerEvents(ref PBPointerEvents pbPointerEventsModel, ref PBUiTransform uiSdkTransformModel, ref CRDTEntity sdkEntity)
        {
            if (pbPointerEventsModel.IsDirty || uiSdkTransformModel.IsDirty)
            {
                uiSdkTransformModel.PointerFilter = PointerFilterMode.PfmBlock;
            }
        }
    }
}
