using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Visibility;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
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
                nftShapeRenderer.ApplyVisibility(visibility.GetVisible());
        }
    }
}
