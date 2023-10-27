using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using Diagnostics.ReportsHandling;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility.Pool;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public class AvatarWearableHide
    {
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

        public static void ComposeHiddenCategoriesOrdered(string bodyShapeId, HashSet<string> forceRender, IWearable[] wearables, int wearableCount, HashSet<string> combinedHidingList)
        {
            var wearablesByCategory = new Dictionary<string, IWearable>();

            for (var i = 0; i < wearableCount; i++)
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
