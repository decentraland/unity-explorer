﻿using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Optimization.Pools;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        internal static readonly ListObjectPool<URN> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly HashSetObjectPool<URN> POINTERS_HASHSET_POOL = new (hashsetInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly ListObjectPool<IWearable> WEARABLES_POOL =
            new (listInstanceDefaultCapacity: PoolConstants.WEARABLES_PER_AVATAR_COUNT, defaultCapacity: PoolConstants.AVATARS_COUNT);

        internal static readonly HashSetObjectPool<string> CATEGORIES_POOL = new (hashsetInstanceDefaultCapacity: WearablesConstants.CATEGORIES_PRIORITY.Count, defaultCapacity: PoolConstants.AVATARS_COUNT);

        internal static readonly Sprite DEFAULT_THUMBNAIL = Sprite.Create(Texture2D.grayTexture, new Rect(0, 0, 1, 1), new Vector2());

        private static readonly URLBuilder URL_BUILDER = new ();

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<string> wearables, IReadOnlyCollection<string> forceRender)
        {
            List<URN> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);

            foreach (string wearable in wearables)
                pointers.Add(wearable);

            return new GetWearablesByPointersIntention(pointers, bodyShape, forceRender);
        }

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> wearables, IReadOnlyCollection<string> forceRender)
        {
            List<URN> pointers = POINTERS_POOL.Get();
            pointers.Add(bodyShape);
            pointers.AddRange(wearables);

            return new GetWearablesByPointersIntention(pointers, bodyShape, forceRender);
        }

        public static void CreateWearableThumbnailPromise(IRealmData realmData, IAvatarAttachment attachment, World world, IPartitionComponent partitionComponent)
        {
            URLPath thumbnailPath = attachment.GetThumbnail();

            if (string.IsNullOrEmpty(thumbnailPath.Value))
            {
                attachment.ThumbnailAssetResult = new StreamableLoadingResult<Sprite>(DEFAULT_THUMBNAIL);
                return;
            }

            URL_BUILDER.Clear();
            URL_BUILDER.AppendDomain(realmData.Ipfs.ContentBaseUrl).AppendPath(thumbnailPath);

            var promise = Promise.Create(world,
                new GetTextureIntention
                {
                    CommonArguments = new CommonLoadingArguments(URL_BUILDER.Build()),
                },
                partitionComponent);

            world.Create(attachment, promise, partitionComponent);
        }

        public static void ExtractVisibleWearables(string bodyShapeId, IReadOnlyList<IWearable> wearables, int wearableCount, ref HideWearablesResolution hideWearablesResolution)
        {
            Dictionary<string, IWearable> wearablesByCategory = DictionaryPool<string, IWearable>.Get();
            List<IWearable> visibleWearables = WEARABLES_POOL.Get();

            for (var i = 0; i < wearableCount; i++) { wearablesByCategory[wearables[i].GetCategory()] = wearables[i]; }

            HashSet<string> hidingList = CATEGORIES_POOL.Get();
            HashSet<string> combinedHidingList = CATEGORIES_POOL.Get();

            for (var index = 0; index < WearablesConstants.CATEGORIES_PRIORITY.Count; index++)
            {
                string priorityCategory = WearablesConstants.CATEGORIES_PRIORITY[index];
                hidingList.Clear();

                //If the category is already on the hidden list, then we dont care about what its trying to hide. This avoid possible cyclic hidden categories
                //Also, if the category is not equipped, then we cant do anything
                if (combinedHidingList.Contains(priorityCategory) || !wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable)) continue;

                wearable.GetHidingList(bodyShapeId, hidingList);

                foreach (string categoryToHide in hidingList)
                    combinedHidingList.Add(categoryToHide);
            }

            if (hideWearablesResolution.ForceRender != null)
                foreach (string category in hideWearablesResolution.ForceRender) { combinedHidingList.Remove(category); }

            foreach (IWearable wearable in wearables)
            {
                if (!combinedHidingList.Contains(wearable.GetCategory()))
                    visibleWearables.Add(wearable);
            }

            hideWearablesResolution.VisibleWearables = visibleWearables;
            hideWearablesResolution.HiddenCategories = combinedHidingList;

            CATEGORIES_POOL.Release(hidingList);
            DictionaryPool<string, IWearable>.Release(wearablesByCategory);
        }
    }
}
