using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using ECS.Abstract;
using ECS.Unity.Groups;

namespace DCL.SDKComponents.TextShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
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
                textShapeRenderer.ApplyVisibility(visibility.Visible);
                visibility.IsDirty = false;
            }
        }
    }
}
