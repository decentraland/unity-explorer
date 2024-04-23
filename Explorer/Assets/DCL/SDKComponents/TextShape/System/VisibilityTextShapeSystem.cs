using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Visibility;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
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
        private void UpdateVisibility(in TextShapeRendererComponent textShapeRenderer, in PBVisibilityComponent visibility)
        {
            if (visibility.IsDirty)
            {
                textShapeRenderer.ApplyVisibility(visibility.GetVisible());
                visibility.IsDirty = false;
            }
        }
    }
}
