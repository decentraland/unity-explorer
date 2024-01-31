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
    public class AvatarWearableHide
    {
        public static readonly Dictionary<string, string> CategoriesToReadable = new ()
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

        private static readonly Dictionary<string, string> bodyPartsMapping = new ()
        {
            { "head", WearablesConstants.Categories.HEAD },
            { "ubody", WearablesConstants.Categories.UPPER_BODY },
            { "lbody", WearablesConstants.Categories.LOWER_BODY },
            { "hands", WearablesConstants.Categories.HANDS },
            { "feet", WearablesConstants.Categories.FEET },
            { "eyes", WearablesConstants.Categories.EYES },
            { "eyebrows", WearablesConstants.Categories.EYEBROWS },
            { "mouth", WearablesConstants.Categories.MOUTH },
        };

        private static readonly HashSet<string> hideCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string GetCategoryHider(string bodyShapeId, string hiddenCategory, List<IWearable> equippedWearables)
        {
            var wearablesByCategory = DictionaryPool<string, IWearable>.Get();

            for (var i = 0; i < equippedWearables.Count; i++)
                wearablesByCategory[equippedWearables[i].GetCategory()] = equippedWearables[i];

            foreach (string priorityCategory in WearablesConstants.CATEGORIES_PRIORITY)
            {
                if (wearablesByCategory.TryGetValue(priorityCategory, out IWearable wearable))
                {
                    hideCategories.Clear();
                    wearable.GetHidingList(bodyShapeId, hideCategories);

                    if (hideCategories.Contains(hiddenCategory))
                        return wearable.GetCategory();
                }
            }
            DictionaryPool<string, IWearable>.Release(wearablesByCategory);

            return string.Empty;
        }

        public static void ComposeHiddenCategoriesOrdered(string bodyShapeId, HashSet<string> forceRender, List<IWearable> wearables, HashSet<string> combinedHidingList)
        {
            combinedHidingList.Clear();
            var wearablesByCategory = new Dictionary<string, IWearable>();

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

            HashSetPool<string>.Release(hidingList);
        }

        public static void HideBodyShape(GameObject bodyShape, HashSet<string> hidingList, HashSet<string> usedCategories)
        {
            //Means that the body shape was hidden
            if (bodyShape == null)
                return;

            using (PoolExtensions.Scope<List<Renderer>> pooledList = bodyShape.GetComponentsInChildrenIntoPooledList<Renderer>(true))
            {
                for (var i = 0; i < pooledList.Value.Count; i++)
                {
                    Renderer renderer = pooledList.Value[i];

                    string name = renderer.name.ToLower();

                    // Support for the old gltf hierarchy for ABs
                    if (name.Contains("primitive"))
                        name = renderer.transform.parent.name.ToLower();

                    var isPartMapped = false;

                    foreach (KeyValuePair<string, string> kvp in bodyPartsMapping)
                    {
                        if (name.Contains(kvp.Key))
                        {
                            renderer.gameObject.SetActive(!(hidingList.Contains(kvp.Value) || usedCategories.Contains(kvp.Value)));
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
}
