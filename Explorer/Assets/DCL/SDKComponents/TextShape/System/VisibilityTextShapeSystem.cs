using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Groups;
using ECS.Unity.Visibility;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ComponentInstantiationGroup))]
    public partial class VisibilityTextShapeSystem : BaseUnityLoopSystem
    {
        public VisibilityTextShapeSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World!);
        }

        [Query]
        private void UpdateVisibility(in TextShapeComponent textComponent, in PBVisibilityComponent visibility)
        {
            if (visibility.IsDirty)
                textComponent.TextMeshPro.enabled = visibility.GetVisible();
        }
    }
}
