using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using ECS;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;


namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class ResolveWearableThumbnailSystem : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;
        private readonly URLBuilder urlBuilder;

        public ResolveWearableThumbnailSystem(World world, IRealmData realmData) : base(world)
        {
            this.realmData = realmData;
            urlBuilder = new URLBuilder();
        }

        protected override void Update(float t)
        {
            StartWearableThumbnailDownloadQuery(World);
            CompleteWearableThumbnailDownloadQuery(World);
        }

        [Query]
        [None(typeof(Promise))]
        private void StartWearableThumbnailDownload(in Entity entity, ref WearableThumbnailComponent wearableThumbnailComponent, ref PartitionComponent partitionComponent)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(wearableThumbnailComponent.Wearable.GetThumbnail());
            Promise promise = Promise.Create(World,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(urlBuilder.Build())
                },
                partitionComponent);

            World.Add(entity, promise);
        }

        [Query]
        private void CompleteWearableThumbnailDownload(in Entity entity, ref WearableThumbnailComponent wearableThumbnailComponent, ref Promise promise)
        {
            if (promise.TryConsume(World, out var result))
            {
                wearableThumbnailComponent.Wearable.WearableThumbnail = result;
                World.Destroy(entity);
            }
        }
    }
}
