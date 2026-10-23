using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using Runtime.Wearables;
using System;
using System.Collections.Generic;

namespace DCL.Backpack.AvatarSection.Randomizer
{
    /// <summary>
    ///     Picks one random wearable per category for a body shape, and resolves picks that hide each other.
    /// </summary>
    public class OutfitRandomizer
    {
        internal const float OPTIONAL_CATEGORY_INCLUDE_CHANCE = 0.75f;
        internal const float SKIN_INCLUDE_CHANCE = 0.2f;
        internal const float CUSTOM_PRIORITY_CHANCE = 0.8f;

        private const string BASE_WEARABLE_URN_PREFIX = "urn:decentraland:off-chain:base-avatars:";

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

        public OutfitRandomizer(Random random)
        {
            this.random = random;
        }

        /// <summary>
        ///     Replaces the catalog with the wearables that fit <paramref name="bodyShape" />. Body shapes and smart wearables are left out.
        /// </summary>
        public void BuildCatalog(IReadOnlyList<ITrimmedWearable> wearables, BodyShape bodyShape)
        {
            catalog.Clear();
            bool isFemale = bodyShape.Equals(BodyShape.FEMALE);

            foreach (ITrimmedWearable wearable in wearables)
            {
                string category = wearable.GetCategory();

                if (category == WearableCategories.Categories.BODY_SHAPE || wearable.IsSmart() || isPendingGift(wearable.GetUrn()))
                    continue;

                if (!wearable.IsCompatibleWithBodyShape(bodyShape))
                    continue;

                if (isFemale && FEMALE_EXCLUDED_CATEGORIES.Contains(category))
                    continue;

                URN urn = wearable.GetUrn();

                if (HasBodyTypePrefix(urn, isFemale ? "m_" : "f_"))
                    continue;

                AddEntry(category, urn);
            }
        }

        /// <summary>
        ///     Fills <paramref name="result" /> with one pick per category. Optional categories and skin are sometimes left empty.
        ///     Where the user owns custom wearables in a category, a custom one is picked <see cref="CUSTOM_PRIORITY_CHANCE" /> of the time.
        /// </summary>
        public void Pick(Dictionary<string, URN> result)
        {
            result.Clear();

            foreach ((string category, CategoryPool pool) in catalog)
            {
                if (random.NextDouble() >= IncludeChance(category))
                    continue;

                if (TryPickFrom(pool, null, out URN urn))
                    result[category] = urn;
            }
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
            var kept = new HashSet<string>();

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
            if (!catalog.TryGetValue(category, out CategoryPool? pool))
            {
                pool = new CategoryPool();
                catalog[category] = pool;
            }

            if (((string)urn).StartsWith(BASE_WEARABLE_URN_PREFIX, StringComparison.Ordinal))
                pool.Base.Add(urn);
            else
                pool.Custom.Add(urn);
        }

        /// <summary>
        ///     Tries the preferred pool first and falls back to the other one, so a category with a single kind of wearable always yields a pick.
        /// </summary>
        private bool TryPickFrom(CategoryPool pool, HashSet<URN>? tried, out URN urn)
        {
            bool preferCustom = random.NextDouble() < CUSTOM_PRIORITY_CHANCE;
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

        private sealed class CategoryPool
        {
            public readonly List<URN> Custom = new ();
            public readonly List<URN> Base = new ();
        }
    }
}
