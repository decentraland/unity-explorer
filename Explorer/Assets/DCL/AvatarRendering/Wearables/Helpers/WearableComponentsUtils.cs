using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        internal static readonly ListObjectPool<URN> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly HashSetObjectPool<URN> POINTERS_HASHSET_POOL = new (hashsetInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly ListObjectPool<IWearable> WEARABLES_POOL =
            new (listInstanceDefaultCapacity: PoolConstants.WEARABLES_PER_AVATAR_COUNT, defaultCapacity: PoolConstants.AVATARS_COUNT);

        internal static readonly HashSetObjectPool<string> CATEGORIES_POOL = new (hashsetInstanceDefaultCapacity: WearablesConstants.CATEGORIES_PRIORITY.Count, defaultCapacity: PoolConstants.AVATARS_COUNT);

        public static readonly Dictionary<string, string> CATEGORIES_TO_READABLE = new ()
        {
            { WearablesConstants.Categories.HEAD, "Head" },
            { WearablesConstants.Categories.UPPER_BODY, "Upper body" },
            { WearablesConstants.Categories.LOWER_BODY, "Lower body" },
            { WearablesConstants.Categories.HANDS, "Hands" },
            { WearablesConstants.Categories.FEET, "Feet" },
            { WearablesConstants.Categories.EYES, "Eyes" },
            { WearablesConstants.Categories.EYEBROWS, "Eyebrows" },
            { WearablesConstants.Categories.MOUTH, "Mouth" },
            { WearablesConstants.Categories.HAT, "Hat" },
            { WearablesConstants.Categories.MASK, "Mask" },
            { WearablesConstants.Categories.HAIR, "Hair" },
            { WearablesConstants.Categories.FACIAL_HAIR, "Facial hair" },
            { WearablesConstants.Categories.SKIN, "Skin" },
            { WearablesConstants.Categories.HANDS_WEAR, "Handwear" },
            { WearablesConstants.Categories.TIARA, "Tiara" },
            { WearablesConstants.Categories.HELMET, "Helmet" },
            { WearablesConstants.Categories.EARRING, "Earring" },
            { WearablesConstants.Categories.EYEWEAR, "Eyewear" },
            { WearablesConstants.Categories.TOP_HEAD, "Top head" },
            { WearablesConstants.Categories.BODY_SHAPE, "Body shape" },
        };

        private static readonly (string, string)[] BODY_PARTS_MAPPING =
        {
            ("head", WearablesConstants.Categories.HEAD),
            ("ubody", WearablesConstants.Categories.UPPER_BODY),
            ("lbody", WearablesConstants.Categories.LOWER_BODY),
            ("hands", WearablesConstants.Categories.HANDS),
            ("feet", WearablesConstants.Categories.FEET), ("eyes", WearablesConstants.Categories.HEAD), ("eyebrows", WearablesConstants.Categories.HEAD), ("mouth", WearablesConstants.Categories.HEAD)
        };

        private static readonly HashSet<string> HIDE_CATEGORIES = new (StringComparer.OrdinalIgnoreCase);

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

        public static void ExtractVisibleWearables(string bodyShapeId,
            IReadOnlyList<IWearable> wearables,
            ref HideWearablesResolution hideWearablesResolution)
        {
            List<IWearable> visibleWearables = WEARABLES_POOL.Get()!;
            HashSet<string> combinedHidingList = CATEGORIES_POOL.Get()!;

            ComposeHiddenCategoriesOrdered(bodyShapeId, hideWearablesResolution.ForceRender, wearables, combinedHidingList);

            foreach (IWearable wearable in wearables)
                if (!combinedHidingList.Contains(wearable.GetCategory()))
                    visibleWearables.Add(wearable);

            hideWearablesResolution.VisibleWearables = visibleWearables;
            hideWearablesResolution.HiddenCategories = combinedHidingList;
        }

        public static void ComposeHiddenCategoriesOrdered(string bodyShapeId,
            IReadOnlyCollection<string>? forceRender,
            IReadOnlyList<IWearable> wearables,
            HashSet<string> combinedHidingList)
        {
            combinedHidingList.Clear();

            Dictionary<string, IWearable> wearablesByCategory = DictionaryPool<string, IWearable>.Get();
            HashSet<string> firstWaveHidden = HashSetPool<string>.Get();
            HashSet<string> hidingList = HashSetPool<string>.Get();

            for (var i = 0; i < wearables.Count; i++)
                wearablesByCategory[wearables[i].GetCategory()] = wearables[i];

            foreach (string priorityCategory in WearablesConstants.CATEGORIES_PRIORITY)
            {
                hidingList.Clear();

                if (!wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable))
                    continue;

                wearable.GetHidingList(bodyShapeId, hidingList);

                foreach (string categoryToHide in hidingList)
                    firstWaveHidden.Add(categoryToHide);
            }

            foreach (string priorityCategory in WearablesConstants.CATEGORIES_PRIORITY)
            {
                hidingList.Clear();

                if (firstWaveHidden.Contains(priorityCategory) ||
                    !wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable))
                    continue;

                wearable.GetHidingList(bodyShapeId, hidingList);

                foreach (string categoryToHide in hidingList)
                    combinedHidingList.Add(categoryToHide);
            }

            if (forceRender != null)
                foreach (string category in forceRender)
                    combinedHidingList.Remove(category);

            DictionaryPool<string, IWearable>.Release(wearablesByCategory);
            HashSetPool<string>.Release(hidingList);
            HashSetPool<string>.Release(firstWaveHidden);
        }

        public static string GetCategoryHider(string bodyShapeId, string hiddenCategory, List<IWearable> equippedWearables)
        {
            using var scope = DictionaryPool<string, IWearable>.Get(out var wearablesByCategory);

            for (var i = 0; i < equippedWearables.Count; i++)
                wearablesByCategory[equippedWearables[i].GetCategory()] = equippedWearables[i];

            foreach (string priorityCategory in WearablesConstants.CATEGORIES_PRIORITY)
                if (wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable))
                {
                    HIDE_CATEGORIES.Clear();
                    wearable.GetHidingList(bodyShapeId, HIDE_CATEGORIES);

                    if (HIDE_CATEGORIES.Contains(hiddenCategory))
                        return wearable.GetCategory();
                }

            return string.Empty;
        }

        public static void HideBodyShape(GameObject? bodyShape, HashSet<string> hidingList, HashSet<string> usedCategories)
        {
            //Means that the body shape was hidden
            if (bodyShape == null)
                return;

            using PoolExtensions.Scope<List<Renderer>> pooledList = bodyShape.GetComponentsInChildrenIntoPooledList<Renderer>(true);

            for (var i = 0; i < pooledList.Value.Count; i++)
            {
                Renderer renderer = pooledList.Value[i];

                string name = renderer.name;

                // Support for the old gltf hierarchy for ABs
                if (name.Contains("primitive", StringComparison.OrdinalIgnoreCase))
                    name = renderer.transform.parent.name;

                var isPartMapped = false;

                foreach ((string key, string value) in BODY_PARTS_MAPPING)
                {
                    if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                    {
                        renderer.gameObject.SetActive(!(hidingList.Contains(value) || usedCategories.Contains(value)));
                        isPartMapped = true;
                        break;
                    }
                }

                if (!isPartMapped)
                    ReportHub.LogWarning(ReportCategory.WEARABLE, $"{name} has not been set-up as a valid body part");
            }
        }
    }
}
