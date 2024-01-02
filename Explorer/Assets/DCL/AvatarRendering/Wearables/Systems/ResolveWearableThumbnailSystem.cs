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
        private const string THUMBNAIL_DEFAULT_KEY = "thumbnail.png";
        private readonly IRealmData realmData;

        public ResolveWearableThumbnailSystem(World world, IRealmData realmData) : base(world)
        {
            this.realmData = realmData;
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
            string thumbnailHash = wearableThumbnailComponent.Wearable.WearableDTO.Asset.metadata.thumbnail;

            if (thumbnailHash == THUMBNAIL_DEFAULT_KEY)
            {
                for (var i = 0; i < wearableThumbnailComponent.Wearable.WearableDTO.Asset.content.Length; i++)
                {
                    if (wearableThumbnailComponent.Wearable.WearableDTO.Asset.content[i].file == THUMBNAIL_DEFAULT_KEY)
                        thumbnailHash = wearableThumbnailComponent.Wearable.WearableDTO.Asset.content[i].hash;
                }
            }

            URLBuilder urlBuilder = new URLBuilder();
            urlBuilder.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(new URLPath(thumbnailHash));
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
