using System.Collections.Generic;
using System.Linq;

namespace Runtime.Wearables
{
    public static class WearableUtils
    {
        /// <summary>
        /// Resolves conflicts in the wearable category hiding system by applying
        /// priority rules and handling force-render overrides.
        /// </summary>
        /// <param name="hiddenCategoriesByCategory">
        /// A dictionary mapping a category name to the set of other categories
        /// it hides.
        /// </param>
        /// <param name="forceRender">
        /// An optional collection of category names that must remain visible
        /// regardless of normal hiding rules. Categories in this collection
        /// will keep their hiding lists, even if hidden by higher priority
        /// categories. Categories not in this collection may have their hiding
        /// lists cleared if they are hidden.
        /// </param>
        /// <param name="combinedHidingList">
        /// An output set that will be populated with the final, flattened list
        /// of all categories that are hidden after conflict resolution.
        /// </param>
        public static void ResolveHidingConflicts(
            Dictionary<string, HashSet<string>> hiddenCategoriesByCategory,
            IReadOnlyCollection<string>? forceRender,
            HashSet<string> combinedHidingList)
        {
            foreach (string category in WearableCategories.CATEGORIES_PRIORITY)
            {
                // if this category is not actually worn or does not have a hiding list skip it
                if (!TryGetNonEmptySet(hiddenCategoriesByCategory, category, out var hiddenList))
                    continue;

                foreach (string hiddenCategory in hiddenList)
                {
                    // if this hidden category is not worn or does not have a back hiding list itself skip it
                    if (!TryGetNonEmptySet(hiddenCategoriesByCategory, hiddenCategory, out var backList))
                        continue;

                    // OPTIONAL - if this hidden category is not forced to render then it should not hide any category
                    if (forceRender != null && !forceRender.Contains(hiddenCategory))
                    {
                        backList.Clear();
                        continue;
                    }

                    // finally remove any hidden category that hides back the current category
                    backList.Remove(category);
                }
            }

            // OPTIONAL - Clear out force rendered categories
            if (forceRender != null)
            {
                foreach (var hiddenCategories in hiddenCategoriesByCategory.Values)
                {
                    foreach (string forced in forceRender)
                        hiddenCategories.Remove(forced);
                }
            }

            GetCombinedHidingList(hiddenCategoriesByCategory, combinedHidingList);
        }

        private static bool TryGetNonEmptySet(
            Dictionary<string, HashSet<string>> dict,
            string key,
            out HashSet<string> set) =>
            dict.TryGetValue(key, out set) && set.Count > 0;

        /// <summary>
        /// Builds a combined list of all categories that are hidden by any other category.
        /// </summary>
        /// <param name="hiddenCategoriesByCategory">
        /// A dictionary where the key is a category name, and the value is the set
        /// of categories that are hidden by that category.
        /// </param>
        /// <param name="combinedHidingList">
        /// A set that will be populated with the union of all hidden categories
        /// across every entry in <paramref name="hiddenCategoriesByCategory"/>.
        /// Existing values in this set will be preserved.
        /// </param>
        private static void GetCombinedHidingList(
            Dictionary<string, HashSet<string>> hiddenCategoriesByCategory,
            HashSet<string> combinedHidingList)
        {
            combinedHidingList.Clear();

            foreach (var hidingList in hiddenCategoriesByCategory.Values)
                combinedHidingList.UnionWith(hidingList);
        }
    }
}