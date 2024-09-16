using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Optimization.Pools;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Threading;
using AssetManagement;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Thumbnails.Utils;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.AvatarRendering.Wearables.Systems.Load;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.AssetBundles;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        internal static readonly ListObjectPool<URN> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly HashSetObjectPool<URN> POINTERS_HASHSET_POOL = new (hashsetInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly ListObjectPool<IWearable> WEARABLES_POOL =
            new (listInstanceDefaultCapacity: PoolConstants.WEARABLES_PER_AVATAR_COUNT, defaultCapacity: PoolConstants.AVATARS_COUNT);

        internal static readonly HashSetObjectPool<string> CATEGORIES_POOL = new (hashsetInstanceDefaultCapacity: WearablesConstants.CATEGORIES_PRIORITY.Count, defaultCapacity: PoolConstants.AVATARS_COUNT);

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<string> wearables, IReadOnlyCollection<string> forceRender)
        {
            List<URN> pointers = POINTERS_POOL.Get()!;
            pointers.Add(bodyShape);

            foreach (string wearable in wearables)
                pointers.Add(wearable);

            return new GetWearablesByPointersIntention(pointers, bodyShape, forceRender);
        }

        public static GetWearablesByPointersIntention CreateGetWearablesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> wearables, IReadOnlyCollection<string> forceRender)
        {
            List<URN> pointers = POINTERS_POOL.Get()!;
            pointers.Add(bodyShape);
            pointers.AddRange(wearables);

            return new GetWearablesByPointersIntention(pointers, bodyShape, forceRender);
        }

        public static void ExtractVisibleWearables(string bodyShapeId, IReadOnlyList<IWearable> wearables, int wearableCount, ref HideWearablesResolution hideWearablesResolution)
        {
            using var _ = DictionaryPool<string, IWearable>.Get(out var wearablesByCategory)!;
            List<IWearable> visibleWearables = WEARABLES_POOL.Get()!;

            for (var i = 0; i < wearableCount; i++) { wearablesByCategory[wearables[i]!.GetCategory()] = wearables[i]; }

            HashSet<string> hidingList = CATEGORIES_POOL.Get()!;
            HashSet<string> combinedHidingList = CATEGORIES_POOL.Get()!;

            for (var index = 0; index < WearablesConstants.CATEGORIES_PRIORITY.Count; index++)
            {
                string priorityCategory = WearablesConstants.CATEGORIES_PRIORITY[index]!;
                hidingList.Clear();

                //If the category is already on the hidden list, then we dont care about what its trying to hide. This avoid possible cyclic hidden categories
                //Also, if the category is not equipped, then we cant do anything
                if (combinedHidingList.Contains(priorityCategory) || !wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable)) continue;

                wearable!.GetHidingList(bodyShapeId, hidingList);

                foreach (string categoryToHide in hidingList)
                    combinedHidingList.Add(categoryToHide);
            }

            if (hideWearablesResolution.ForceRender != null)
                foreach (string category in hideWearablesResolution.ForceRender)
                    combinedHidingList.Remove(category);

            foreach (IWearable wearable in wearables)
                if (!combinedHidingList.Contains(wearable.GetCategory()))
                    visibleWearables.Add(wearable);

            hideWearablesResolution.VisibleWearables = visibleWearables;
            hideWearablesResolution.HiddenCategories = combinedHidingList;

            CATEGORIES_POOL.Release(hidingList);
        }


        public static void SetAssetResult(this IWearable wearable, BodyShape bodyShape, int index, StreamableLoadingResult<AttachmentAssetBase> wearableResult)
        {
            ref var asset = ref wearable.WearableAssetResults[bodyShape];
            asset.Results[index] = wearableResult;
        }
    }
}
