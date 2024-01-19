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
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        internal static readonly ListObjectPool<string> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);
        internal static readonly ArrayPool<IWearable> RESULTS_POOL = ArrayPool<IWearable>.Create(20, 20);

        private static readonly URLBuilder URL_BUILDER = new ();
        private static readonly Sprite DEFAULT_THUMBNAIL = Sprite.Create(Texture2D.grayTexture, new Rect(0, 0, 1, 1), new Vector2());

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<string> wearables, IReadOnlyCollection<string> forceRender)
        {
            List<string> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);
            pointers.AddRange(wearables);

            IWearable[] results = RESULTS_POOL.Rent(pointers.Count);
            return new GetWearablesByPointersIntention(pointers, results, bodyShape, forceRender);
        }

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> wearables, IReadOnlyCollection<string> forceRender)
        {
            List<string> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);

            foreach (URN urn in wearables)
                pointers.Add(urn);

            IWearable[] results = RESULTS_POOL.Rent(pointers.Count);
            return new GetWearablesByPointersIntention(pointers, results, bodyShape, forceRender);
        }

        public static void CreateWearableThumbnailPromise(IRealmData realmData, IWearable wearable, World world, IPartitionComponent partitionComponent)
        {
            URLPath thumbnailPath = wearable.GetThumbnail();

            if (string.IsNullOrEmpty(thumbnailPath.Value))
            {
                wearable.WearableThumbnail = new StreamableLoadingResult<Sprite>(DEFAULT_THUMBNAIL);
                return;
            }

            URL_BUILDER.Clear();
            URL_BUILDER.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(thumbnailPath);

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(URL_BUILDER.Build())
                },
                partitionComponent);

            world.Create(wearable, promise, partitionComponent);
        }

        public static void ExtractVisibleWearables(string bodyShapeId, IReadOnlyCollection<string> forceRender, IWearable[] wearables, int wearableCount, List<IWearable> visibleWearables)
        {
            var wearablesByCategory = new Dictionary<string, IWearable>();

            for (var i = 0; i < wearableCount; i++)
            {
                wearablesByCategory.Add(wearables[i].GetCategory(), wearables[i]);
            }

            HashSet<string> hidingList = HashSetPool<string>.Get();

            HashSet<string> combinedHidingList = new HashSet<string>();

            foreach (string priorityCategory in WearablesConstants.CATEGORIES_PRIORITY)
            {
                hidingList.Clear();

                //If the category is already on the hidden list, then we dont care about what its trying to hide. This avoid possible cyclic hidden categories
                //Also, if the category is not equipped, then we cant do anything
                if (combinedHidingList.Contains(priorityCategory) || !wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable)) continue;

                wearable.GetHidingList(bodyShapeId, hidingList);

                foreach (string categoryToHide in hidingList)
                    combinedHidingList.Add(categoryToHide);
            }

            if (forceRender != null)
                foreach (string category in forceRender) { combinedHidingList.Remove(category); }

            foreach (var wearable in wearables)
            {
                if(!combinedHidingList.Contains(wearable.GetCategory()))
                    visibleWearables.Add(wearable);
            }

            HashSetPool<string>.Release(hidingList);
        }
    }
}
