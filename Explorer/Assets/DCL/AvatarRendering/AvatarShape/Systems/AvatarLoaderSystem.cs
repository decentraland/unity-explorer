using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.ColorComponent;
using System;
using System.Collections.Generic;
using Entity = Arch.Core.Entity;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    /// <summary>
    ///     Start loading the avatar shape for the entity from <see cref="Profile" /> or <see cref="PBAvatarShape" /> components.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarLoaderSystem : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;

        internal AvatarLoaderSystem(World world, IRealmData realmData) : base(world)
        {
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            CreateAvatarShapeFromSDKComponentQuery(World);
            UpdateAvatarFromSDKComponentQuery(World);

            CreateAvatarShapeFromProfileQuery(World);
            UpdateAvatarFromProfileQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(Profile))]
        private void CreateAvatarShapeFromSDKComponent(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);
            EmotePromise emotePromise = CreateEmotePromise(pbAvatarShape, partition);
            pbAvatarShape.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise, pbAvatarShape.SkinColor.ToUnityColor(), pbAvatarShape.HairColor.ToUnityColor(), emotePromise));
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition);
            EmotePromise emotePromise = CreateEmotePromise(profile, partition);
            profile.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, emotePromise));
        }

        [Query]
        [None(typeof(Profile))]
        private void UpdateAvatarFromSDKComponent(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(pbAvatarShape, partition);
            avatarShapeComponent.WearablePromise = newPromise;
            avatarShapeComponent.EmotePromise = CreateEmotePromise(pbAvatarShape, partition);

            avatarShapeComponent.BodyShape = pbAvatarShape;
            avatarShapeComponent.IsDirty = true;
            pbAvatarShape.IsDirty = false;
        }

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateAvatarFromProfile(ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(profile, partition);
            avatarShapeComponent.WearablePromise = newPromise;
            avatarShapeComponent.EmotePromise = CreateEmotePromise(profile, partition);

            avatarShapeComponent.BodyShape = profile.Avatar.BodyShape;
            avatarShapeComponent.IsDirty = true;
            profile.IsDirty = false;
        }

        private WearablePromise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            WearablePromise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(pbAvatarShape, pbAvatarShape.Wearables, Array.Empty<string>()),
                partition);

        private WearablePromise CreateWearablePromise(Profile profile, PartitionComponent partition) =>
            // profile.Avatar.Wearables should be shortened, but since GetWearablesByPointers already retrieves shortened-urns,
            // there is not need to convert
            WearablePromise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Wearables, profile.Avatar.ForceRender),
                partition);

        private EmotePromise CreateEmotePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition)
        {
            var intention = new GetEmotesByPointersIntention(pbAvatarShape.Wearables, new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint));
            return EmotePromise.Create(World, intention, partition);
        }

        private EmotePromise CreateEmotePromise(Profile profile, PartitionComponent partition)
        {
            // TODO: delete tmp list
            var tmp = new List<string>();

            foreach (URN urn in profile.Avatar.Wearables)
                tmp.Add(urn);

            // TODO: should we avoid setting the url from this system?
            var intention = new GetEmotesByPointersIntention(tmp, new CommonLoadingArguments(realmData.Ipfs.EntitiesActiveEndpoint));
            return EmotePromise.Create(World, intention, partition);
        }
    }
}
