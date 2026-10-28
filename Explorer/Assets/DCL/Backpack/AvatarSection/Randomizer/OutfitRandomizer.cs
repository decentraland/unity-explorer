using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Backpack.AvatarSection.Randomizer
{
    /// <summary>
    ///     Picks one random wearable per category for a body shape, and resolves picks that hide each other.
    ///     The catalog holds every base wearable plus the pages of owned wearables fetched for the current roll.
    /// </summary>
    public class OutfitRandomizer
    {
        /// <summary>
        ///     Chance that an optional accessory category (hat, mask, earring, facial hair, …) gets a pick on a roll.
        /// </summary>
        internal const float OPTIONAL_CATEGORY_INCLUDE_CHANCE = 0.75f;

        /// <summary>
        ///     Chance that a roll includes a skin, kept low because a skin covers most of the outfit.
        /// </summary>
        internal const float SKIN_INCLUDE_CHANCE = 0.2f;

        /// <summary>
        ///     Chance that a category picks from the user's own wearables before falling back to the base ones.
        /// </summary>
        internal const float CUSTOM_PRIORITY_CHANCE = 0.8f;

        private const string MALE_BASE_PREFIX = "m_";
        private const string FEMALE_BASE_PREFIX = "f_";

        /// <summary>
        ///     Every category a roll can fill, in hiding priority order.
        /// </summary>
        public static readonly IReadOnlyList<string> CATEGORIES = OutfitCategories();

        private static readonly Func<URN, bool> NOT_PENDING = _ => false;

        private static readonly HashSet<string> OPTIONAL_CATEGORIES = new ()
        {
            WearableCategories.Categories.FACIAL_HAIR,
            WearableCategories.Categories.HAT,
            WearableCategories.Categories.MASK,
            WearableCategories.Categories.TIARA,
            WearableCategories.Categories.HELMET,
            WearableCategories.Categories.EARRING,
            WearableCategories.Categories.EYEWEAR,
            WearableCategories.Categories.TOP_HEAD,
            WearableCategories.Categories.HANDS_WEAR,
        };

        private static readonly HashSet<string> FEMALE_EXCLUDED_CATEGORIES = new ()
        {
            WearableCategories.Categories.FACIAL_HAIR,
        };

        private readonly Dictionary<string, CategoryPool> catalog = new ();
        private readonly Random random;
        private BodyShape bodyShape = BodyShape.MALE;
        private Func<URN, bool> isPendingGift = NOT_PENDING;

        private bool isFemale => bodyShape.Equals(BodyShape.FEMALE);

        public OutfitRandomizer(Random random)
        {
            this.random = random;
        }

        /// <summary>
        ///     Replaces the catalog with the base wearables that fit <paramref name="bodyShape" />, dropping the owned pages of the previous roll.
        ///     Body shapes, smart wearables and wearables whose every owned copy is pending a gift are left out, here and in <see cref="AddCustomPage" />.
        /// </summary>
        public void BuildCatalog(IReadOnlyList<ITrimmedWearable> baseWearables, BodyShape bodyShape, Func<URN, bool> isPendingGift)
        {
            foreach (CategoryPool pool in catalog.Values)
                pool.Clear();

            this.bodyShape = bodyShape;
            this.isPendingGift = isPendingGift;

            foreach (ITrimmedWearable wearable in baseWearables)
            {
                string category = wearable.GetCategory();

                if (IsUsable(wearable, category, out URN urn))
                    AddEntry(category, urn);
            }
        }

        /// <summary>
        ///     Fills <paramref name="chosen" /> with the categories this roll fills: every core category and, by their odds, the optional ones and skin.
        ///     A category is a candidate when the catalog has wearables for it or <paramref name="ownedTotals" /> reports owned ones.
        /// </summary>
        public void ChooseCategories(IReadOnlyDictionary<string, int> ownedTotals, HashSet<string> chosen)
        {
            chosen.Clear();

            foreach (string category in CATEGORIES)
            {
                if (isFemale && FEMALE_EXCLUDED_CATEGORIES.Contains(category))
                    continue;

                bool hasEntries = catalog.TryGetValue(category, out CategoryPool? pool) && pool.HasEntries;
                bool hasOwned = ownedTotals.TryGetValue(category, out int total) && total > 0;

                if (!hasEntries && !hasOwned)
                    continue;

                if (random.NextDouble() >= IncludeChance(category))
                    continue;

                chosen.Add(category);
            }
        }

        /// <summary>
        ///     A uniform random index in [0, <paramref name="count" />).
        /// </summary>
        public int PickIndex(int count) =>
            random.Next(count);

        /// <summary>
        ///     Adds the usable wearables of <paramref name="category" /> from a fetched page of owned wearables.
        ///     The one at <paramref name="preferredIndex" />, when usable, is the first pick for the category whenever custom is preferred.
        /// </summary>
        public void AddCustomPage(string category, IReadOnlyList<ITrimmedWearable> page, int preferredIndex)
        {
            for (var i = 0; i < page.Count; i++)
            {
                ITrimmedWearable wearable = page[i];

                if (wearable.GetCategory() != category || !IsUsable(wearable, category, out URN urn))
                    continue;

                // The base wearables are already in the catalog
                if (urn.IsBaseWearable())
                    continue;

                CategoryPool pool = GetOrCreatePool(category);

                if (i == preferredIndex)
                    pool.PreferredCustom = pool.Custom.Count;

                pool.Custom.Add(urn);
            }
        }

        /// <summary>
        ///     Fills <paramref name="result" /> with one pick per category in <paramref name="chosen" /> that has wearables.
        ///     Where the catalog has custom wearables in a category, a custom one is picked <see cref="CUSTOM_PRIORITY_CHANCE" /> of the time.
        /// </summary>
        public void Pick(IReadOnlyCollection<string> chosen, Dictionary<string, URN> result)
        {
            result.Clear();

            foreach (string category in chosen)
                if (catalog.TryGetValue(category, out CategoryPool? pool) && TryPickFrom(pool, null, out URN urn))
                    result[category] = urn;
        }

        /// <summary>
        ///     Picks another wearable for <paramref name="category" /> that is not in <paramref name="tried" />, with the same custom priority as <see cref="Pick" />.
        /// </summary>
        public bool TryRepick(string category, HashSet<URN> tried, out URN urn)
        {
            urn = default;
            return catalog.TryGetValue(category, out CategoryPool? pool) && TryPickFrom(pool, tried, out urn);
        }

        /// <summary>
        ///     Walks the picks in hiding priority order and keeps a pick only when no kept pick hides it and it hides no kept pick.
        ///     <paramref name="hidden" /> receives every category the kept picks hide.
        /// </summary>
        public static void ResolveConflicts(IReadOnlyDictionary<string, HashSet<string>> hidesByCategory, HashSet<string> dropped, HashSet<string> hidden)
        {
            dropped.Clear();
            hidden.Clear();
            using PooledObject<HashSet<string>> _ = HashSetPool<string>.Get(out HashSet<string> kept);

            foreach (string category in WearableCategories.CATEGORIES_PRIORITY)
            {
                if (!hidesByCategory.TryGetValue(category, out HashSet<string>? hides))
                    continue;

                if (hidden.Contains(category) || hides.Overlaps(kept))
                {
                    dropped.Add(category);
                    continue;
                }

                kept.Add(category);
                hidden.UnionWith(hides);
            }
        }

        /// <summary>
        ///     Core categories are the ones a roll always tries to fill.
        /// </summary>
        public static bool IsCoreCategory(string category) =>
            category != WearableCategories.Categories.SKIN && !OPTIONAL_CATEGORIES.Contains(category);

        internal void AddEntry(string category, URN urn)
        {
            CategoryPool pool = GetOrCreatePool(category);

            if (urn.IsBaseWearable())
                pool.Base.Add(urn);
            else
                pool.Custom.Add(urn);
        }

        private CategoryPool GetOrCreatePool(string category)
        {
            if (!catalog.TryGetValue(category, out CategoryPool? pool))
            {
                pool = new CategoryPool();
                catalog[category] = pool;
            }

            return pool;
        }

        private bool IsUsable(ITrimmedWearable wearable, string category, out URN urn)
        {
            urn = wearable.GetUrn();

            if (category == WearableCategories.Categories.BODY_SHAPE || wearable.IsSmart() || isPendingGift(urn))
                return false;

            if (!wearable.IsCompatibleWithBodyShape(bodyShape))
                return false;

            if (isFemale && FEMALE_EXCLUDED_CATEGORIES.Contains(category))
                return false;

            // Only base avatars encode the body type in their id; other ids are free text
            return !urn.IsBaseWearable() || !HasBodyTypePrefix(urn, isFemale ? MALE_BASE_PREFIX : FEMALE_BASE_PREFIX);
        }

        /// <summary>
        ///     Tries the preferred pool first and falls back to the other one, so a category with a single kind of wearable always yields a pick.
        ///     A first pick that prefers custom takes the custom wearable its page was fetched for, so every owned wearable is equally likely.
        /// </summary>
        private bool TryPickFrom(CategoryPool pool, HashSet<URN>? tried, out URN urn)
        {
            bool preferCustom = random.NextDouble() < CUSTOM_PRIORITY_CHANCE;

            if (preferCustom && tried == null && pool.PreferredCustom >= 0)
            {
                urn = pool.Custom[pool.PreferredCustom];
                return true;
            }

            List<URN> preferred = preferCustom ? pool.Custom : pool.Base;
            List<URN> fallback = preferCustom ? pool.Base : pool.Custom;

            return TryPickUntried(preferred, tried, out urn) || TryPickUntried(fallback, tried, out urn);
        }

        private bool TryPickUntried(List<URN> urns, HashSet<URN>? tried, out URN urn)
        {
            urn = default;
            var remaining = 0;

            foreach (URN candidate in urns)
                if (tried == null || !tried.Contains(candidate))
                    remaining++;

            if (remaining == 0)
                return false;

            int target = random.Next(remaining);

            foreach (URN candidate in urns)
            {
                if (tried != null && tried.Contains(candidate))
                    continue;

                if (target-- == 0)
                {
                    urn = candidate;
                    return true;
                }
            }

            return false;
        }

        private static float IncludeChance(string category)
        {
            if (category == WearableCategories.Categories.SKIN)
                return SKIN_INCLUDE_CHANCE;

            return OPTIONAL_CATEGORIES.Contains(category) ? OPTIONAL_CATEGORY_INCLUDE_CHANCE : 1f;
        }

        private static bool HasBodyTypePrefix(URN urn, string prefix)
        {
            string urnStr = urn;
            int lastColon = urnStr.LastIndexOf(':');

            if (lastColon < 0 || lastColon >= urnStr.Length - 1)
                return false;

            return urnStr.AsSpan(lastColon + 1).StartsWith(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private static string[] OutfitCategories()
        {
            var categories = new List<string>(WearableCategories.CATEGORIES_PRIORITY);
            categories.Remove(WearableCategories.Categories.BODY_SHAPE);
            return categories.ToArray();
        }

        private sealed class CategoryPool
        {
            public readonly List<URN> Custom = new ();
            public readonly List<URN> Base = new ();

            /// <summary>
            ///     Index in <see cref="Custom" /> of the wearable its page was fetched for, or -1.
            /// </summary>
            public int PreferredCustom = -1;

            public bool HasEntries => Custom.Count > 0 || Base.Count > 0;

            public void Clear()
            {
                Custom.Clear();
                Base.Clear();
                PreferredCustom = -1;
            }
        }
    }
}
