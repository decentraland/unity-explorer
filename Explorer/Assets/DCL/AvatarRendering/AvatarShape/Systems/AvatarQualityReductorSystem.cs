using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using ECS.Abstract;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Loading.Assets;
using DCL.Profiles;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    public partial class AvatarQualityReductorSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_AVATARS_QUERY_FULL_QUALITY = new QueryDescription()
            .WithAll<AvatarBase, AvatarShapeComponent>()
            .WithNone<PlayerComponent, AvatarQualityReduced>();

        private static readonly QueryDescription ALL_AVATARS_QUERY_REDUCED_QUALITY = new QueryDescription()
            .WithAll<AvatarBase, AvatarShapeComponent, AvatarQualityReduced, Profile>()
            .WithNone<PlayerComponent>();

        private readonly IAttachmentsAssetsCache wearableAssetsCache;

        public AvatarQualityReductorSystem(World world, IAttachmentsAssetsCache wearableAssetsCache) : base(world)
        {
            this.wearableAssetsCache = wearableAssetsCache;
        }

        protected override void Update(float t)
        {
            TryReduceQualityQuery(World);
            TryIncreaseQualityQuery(World);
        }

        [Query]
        private void TryReduceQuality(in Entity entity, ref AvatarQualityReductionRequest reductionRequest)
        {
            if (!reductionRequest.Reduce) return;

            //TODO: Use frame budget?
            World.Query(ALL_AVATARS_QUERY_FULL_QUALITY, (Entity entity, ref AvatarBase avatarBase, ref AvatarShapeComponent avatarShapeComponent) =>
            {
                AvatarReducedQuality(ref avatarShapeComponent, avatarBase);
                World.Add(entity, new AvatarQualityReduced());
            });
            World.Destroy(entity);
        }

        [Query]
        private void TryIncreaseQuality(in Entity entity, ref AvatarQualityReductionRequest reductionRequest)
        {
            if (reductionRequest.Reduce) return;

            //TODO: Use frame budget?
            World.Query(ALL_AVATARS_QUERY_REDUCED_QUALITY, (Entity entity, ref Profile profile) =>
            {
                profile.IsDirty = true;
                World.Remove<AvatarQualityReduced>(entity);
            });
            World.Destroy(entity);
        }

        private void AvatarReducedQuality(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase)
        {
            if (avatarShapeComponent.WearablePromise.IsConsumed)
            {
                avatarShapeComponent.OutlineCompatibleRenderers.Clear();
                wearableAssetsCache.ReleaseAssets(avatarShapeComponent.InstantiatedWearables);
            }
            else
                avatarShapeComponent.Dereference();

            avatarBase.GhostRenderer.SetActive(true);
        }

    }
}
