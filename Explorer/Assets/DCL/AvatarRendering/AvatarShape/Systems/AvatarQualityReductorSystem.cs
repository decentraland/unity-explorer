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
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.DeferredLoading.Components;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    public partial class AvatarQualityReductorSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription ALL_AVATARS_QUERY_FULL_QUALITY = new QueryDescription()
            .WithAll<AvatarBase, AvatarShapeComponent>()
            .WithNone<PlayerComponent, AvatarQualityReducedComponent>();

        private static readonly QueryDescription ALL_AVATARS_QUERY_REDUCED_QUALITY = new QueryDescription()
            .WithAll<AvatarBase, AvatarShapeComponent, AvatarQualityReducedComponent, Profile>()
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
        private void TryReduceQuality(in Entity entity, ref QualityChangeRequest reductionRequest)
        {
            if (reductionRequest.Domain != QualityReductionRequestDomain.AVATAR) return;

            if (!reductionRequest.IsReduce()) return;

            //TODO: Use frame budget?
            World.Query(ALL_AVATARS_QUERY_FULL_QUALITY, (Entity entity, ref AvatarBase avatarBase, ref AvatarShapeComponent avatarShapeComponent) =>
            {
                ReduceAvatarQuality(ref avatarShapeComponent, avatarBase);
                World.Add(entity, new AvatarQualityReducedComponent());
            });
            World.Destroy(entity);
        }

        [Query]
        private void TryIncreaseQuality(in Entity entity, ref QualityChangeRequest reductionRequest)
        {
            if (reductionRequest.Domain != QualityReductionRequestDomain.AVATAR) return;

            if (reductionRequest.IsReduce()) return;

            //TODO: Use frame budget?
            World.Query(ALL_AVATARS_QUERY_REDUCED_QUALITY, (Entity entity, ref Profile profile, ref AvatarBase avatarBase) =>
            {
                IncreaseAvatarQuality(ref profile, avatarBase);
                World.Remove<AvatarQualityReducedComponent>(entity);
            });
            World.Destroy(entity);
        }

        private static void IncreaseAvatarQuality(ref Profile profile, AvatarBase avatarBase)
        {
            profile.IsDirty = true;
            avatarBase.GhostRenderer.SetActive(false);
        }

        private void ReduceAvatarQuality(ref AvatarShapeComponent avatarShapeComponent, AvatarBase avatarBase)
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
