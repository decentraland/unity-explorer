using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Optimization.Pools;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        internal static readonly ListObjectPool<string> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        internal static readonly ArrayPool<IWearable> RESULTS_POOL = ArrayPool<IWearable>.Create(20, 20);
        internal static readonly URLBuilder URL_BUILDER = new URLBuilder();

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<string> wearables)
        {
            List<string> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);
            pointers.AddRange(wearables);

            IWearable[] results = RESULTS_POOL.Rent(pointers.Count);
            return new GetWearablesByPointersIntention(pointers, results, bodyShape);
        }

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> wearables)
        {
            List<string> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);

            foreach (URN urn in wearables)
                pointers.Add(urn);

            IWearable[] results = RESULTS_POOL.Rent(pointers.Count);
            return new GetWearablesByPointersIntention(pointers, results, bodyShape);
        }

        public static void CreateWearableThumbnailPromise(IRealmData realmData, IWearable wearable, World world, IPartitionComponent partitionComponent)
        {
            Debug.Log($"wearable thumbnail {wearable.GetThumbnail()} and {wearable.GetUrn()} and {wearable.GetName()}");
            if (string.IsNullOrEmpty(wearable.GetThumbnail().Value))
            {
                wearable.WearableThumbnail = new StreamableLoadingResult<Sprite>(Sprite.Create(Texture2D.grayTexture, new Rect(0, 0, 1, 1), new Vector2()));
                return;
            }

            URL_BUILDER.Clear();
            URL_BUILDER.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(wearable.GetThumbnail());

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(URL_BUILDER.Build())
                },
                partitionComponent);

            world.Create(wearable, promise, partitionComponent);
        }
    }
}
