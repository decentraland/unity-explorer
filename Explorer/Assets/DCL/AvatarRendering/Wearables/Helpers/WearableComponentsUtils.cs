using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearableComponentsUtils
    {
        public static readonly ListObjectPool<URN> POINTERS_POOL = new (listInstanceDefaultCapacity: 10, defaultCapacity: 20);

        internal static readonly HashSetObjectPool<URN> POINTERS_HASHSET_POOL = new (hashsetInstanceDefaultCapacity: 10, defaultCapacity: 20);

        public static readonly ListObjectPool<IWearable> WEARABLES_POOL =
            new (listInstanceDefaultCapacity: PoolConstants.WEARABLES_PER_AVATAR_COUNT, defaultCapacity: PoolConstants.AVATARS_COUNT);

        internal static readonly HashSetObjectPool<string> CATEGORIES_POOL = new (hashsetInstanceDefaultCapacity: WearableCategories.CATEGORIES_PRIORITY.Count, defaultCapacity: PoolConstants.AVATARS_COUNT);

        public static readonly Dictionary<string, string> CATEGORIES_TO_READABLE = new ()
        {
            { WearableCategories.Categories.HEAD, "Head" },
            { WearableCategories.Categories.UPPER_BODY, "Upper body" },
            { WearableCategories.Categories.LOWER_BODY, "Lower body" },
            { WearableCategories.Categories.HANDS, "Hands" },
            { WearableCategories.Categories.FEET, "Feet" },
            { WearableCategories.Categories.EYES, "Eyes" },
            { WearableCategories.Categories.EYEBROWS, "Eyebrows" },
            { WearableCategories.Categories.MOUTH, "Mouth" },
            { WearableCategories.Categories.HAT, "Hat" },
            { WearableCategories.Categories.MASK, "Mask" },
            { WearableCategories.Categories.HAIR, "Hair" },
            { WearableCategories.Categories.FACIAL_HAIR, "Facial hair" },
            { WearableCategories.Categories.SKIN, "Skin" },
            { WearableCategories.Categories.HANDS_WEAR, "Handwear" },
            { WearableCategories.Categories.TIARA, "Tiara" },
            { WearableCategories.Categories.HELMET, "Helmet" },
            { WearableCategories.Categories.EARRING, "Earring" },
            { WearableCategories.Categories.EYEWEAR, "Eyewear" },
            { WearableCategories.Categories.TOP_HEAD, "Top head" },
            { WearableCategories.Categories.BODY_SHAPE, "Body shape" },
        };

        private static readonly (string, string)[] BODY_PARTS_MAPPING =
        {
            ("head", WearableCategories.Categories.HEAD),
            ("ubody", WearableCategories.Categories.UPPER_BODY),
            ("lbody", WearableCategories.Categories.LOWER_BODY),
            ("hands", WearableCategories.Categories.HANDS),
            ("feet", WearableCategories.Categories.FEET), ("eyes", WearableCategories.Categories.HEAD), ("eyebrows", WearableCategories.Categories.HEAD), ("mouth", WearableCategories.Categories.HEAD)
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

        public static void ComposeHiddenCategoriesOrdered(
            string bodyShapeId,
            IReadOnlyCollection<string>? forceRender,
            IReadOnlyList<IWearable> wearables,
            HashSet<string> combinedHidingList)
        {
            combinedHidingList.Clear();

            Dictionary<string, HashSet<string>> hiddenCategoriesByCategory = DictionaryPool<string, HashSet<string>>.Get();

            for (var i = 0; i < wearables.Count; i++)
            {
                HashSet<string> hidingList = HashSetPool<string>.Get();
                wearables[i].GetHidingList(bodyShapeId, hidingList);
                hiddenCategoriesByCategory[wearables[i].GetCategory()] = hidingList;
            }

            WearableUtils.ResolveHidingConflicts(hiddenCategoriesByCategory, forceRender, combinedHidingList);

            foreach (var hidingList in hiddenCategoriesByCategory.Values)
                HashSetPool<string>.Release(hidingList);

            DictionaryPool<string, HashSet<string>>.Release(hiddenCategoriesByCategory);
        }

        public static string GetCategoryHider(string bodyShapeId, string hiddenCategory, List<IWearable> equippedWearables)
        {
            using var scope = DictionaryPool<string, IWearable>.Get(out var wearablesByCategory);

            for (var i = 0; i < equippedWearables.Count; i++)
                wearablesByCategory[equippedWearables[i].GetCategory()] = equippedWearables[i];

            var hiddenSoFar = HashSetPool<string>.Get();

            foreach (string priorityCategory in WearableCategories.CATEGORIES_PRIORITY)
            {
                if (ShouldSkipCategory(priorityCategory, hiddenSoFar, wearablesByCategory, out var wearable))
                    continue;

                HIDE_CATEGORIES.Clear();
                wearable.GetHidingList(bodyShapeId, HIDE_CATEGORIES);

                foreach (var category in HIDE_CATEGORIES)
                    hiddenSoFar.Add(category);

                if (HIDE_CATEGORIES.Contains(hiddenCategory))
                {
                    ReleaseHiddenSoFar();
                    return wearable.GetCategory();
                }
            }

            ReleaseHiddenSoFar();

            return string.Empty;

            void ReleaseHiddenSoFar() =>
                HashSetPool<string>.Release(hiddenSoFar);
        }

        /// <summary>
        /// Determines whether a category should be skipped during wearable processing.
        /// Skips if the category is already hidden, or if no wearable exists for it.
        /// </summary>
        /// <param name="category">The category being checked.</param>
        /// <param name="hiddenCategories">Set of categories already marked as hidden.</param>
        /// <param name="wearablesByCategory">Lookup of equipped wearables by category.</param>
        /// <param name="wearable">The wearable found for the category, if any.</param>
        /// <returns>True if the category should be skipped, otherwise false.</returns>
        private static bool ShouldSkipCategory(
            string category,
            HashSet<string> hiddenCategories,
            Dictionary<string, IWearable> wearablesByCategory,
            out IWearable wearable)
        {
            if (hiddenCategories.Contains(category))
            {
                wearable = default!;
                return true;
            }

            if (!wearablesByCategory.TryGetValue(category, out wearable))
                return true;

            return false;
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

        public static void ConfirmWearableVisibility(BodyShape bodyShape, ref HideWearablesResolution hideWearablesResolution)
        {
            List<IWearable> helperWearableList = WEARABLES_POOL.Get()!;

            foreach (IWearable visibleWearable in hideWearablesResolution.VisibleWearables)
            {
                if (visibleWearable.WearableAssetResults[bodyShape].Results[0].Value.Succeeded)
                    helperWearableList.Add(visibleWearable);
            }

            // Clean up existing pooled objects before overwriting
            hideWearablesResolution.Release();

            ExtractVisibleWearables(bodyShape, helperWearableList, ref hideWearablesResolution);
            WEARABLES_POOL.Release(helperWearableList);
        }
    }
}
