using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using ECS.Abstract;
using ECS.Unity.Groups;

namespace DCL.SDKComponents.NftShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [ThrottlingEnabled]
    public partial class VisibilityNftShapeSystem : BaseUnityLoopSystem
    {
        public VisibilityNftShapeSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateVisibilityQuery(World!);
        }

        [Query]
        private void UpdateVisibility(in NftShapeRendererComponent nftShapeRenderer, in PBVisibilityComponent visibility)
        {
            if (visibility.IsDirty)
            {
                nftShapeRenderer.ApplyVisibility(visibility.Visible);
                visibility.IsDirty = false;
            }
        }
    }
}
