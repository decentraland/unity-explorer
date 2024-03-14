using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class AvatarWearableHide
    {
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

        public static string GetCategoryHider(string bodyShapeId, string hiddenCategory, List<IWearable> equippedWearables)
        {
            Dictionary<string, IWearable> wearablesByCategory = DictionaryPool<string, IWearable>.Get();

            for (var i = 0; i < equippedWearables.Count; i++)
                wearablesByCategory[equippedWearables[i].GetCategory()] = equippedWearables[i];

            foreach (string priorityCategory in WearablesConstants.CATEGORIES_PRIORITY)
            {
                if (wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable))
                {
                    HIDE_CATEGORIES.Clear();
                    wearable.GetHidingList(bodyShapeId, HIDE_CATEGORIES);

                    if (HIDE_CATEGORIES.Contains(hiddenCategory))
                        return wearable.GetCategory();
                }
            }

            DictionaryPool<string, IWearable>.Release(wearablesByCategory);

            return string.Empty;
        }

        public static void ComposeHiddenCategoriesOrdered(string bodyShapeId, HashSet<string> forceRender, List<IWearable> wearables, HashSet<string> combinedHidingList)
        {
            combinedHidingList.Clear();
            Dictionary<string, IWearable> wearablesByCategory = DictionaryPool<string, IWearable>.Get();

            for (var i = 0; i < wearables.Count; i++)
                wearablesByCategory[wearables[i].GetCategory()] = wearables[i];

            HashSet<string> hidingList = HashSetPool<string>.Get();

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

            DictionaryPool<string, IWearable>.Release(wearablesByCategory);
            HashSetPool<string>.Release(hidingList);
        }

        public static void HideBodyShape(GameObject bodyShape, HashSet<string> hidingList, HashSet<string> usedCategories)
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
