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

        private readonly Dictionary<string, List<URN>> catalog = new ();
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

                if (category == WearableCategories.Categories.BODY_SHAPE || wearable.IsSmart())
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
        /// </summary>
        public void Pick(Dictionary<string, URN> result)
        {
            result.Clear();

            foreach ((string category, List<URN> urns) in catalog)
            {
                if (urns.Count == 0)
                    continue;

                if (random.NextDouble() >= IncludeChance(category))
                    continue;

                result[category] = urns[random.Next(urns.Count)];
            }
        }

        /// <summary>
        ///     Picks another wearable for <paramref name="category" /> that is not in <paramref name="tried" />.
        /// </summary>
        public bool TryRepick(string category, HashSet<URN> tried, out URN urn)
        {
            urn = default;

            if (!catalog.TryGetValue(category, out List<URN>? urns))
                return false;

            int remaining = 0;

            foreach (URN candidate in urns)
                if (!tried.Contains(candidate))
                    remaining++;

            if (remaining == 0)
                return false;

            int target = random.Next(remaining);

            foreach (URN candidate in urns)
            {
                if (tried.Contains(candidate))
                    continue;

                if (target-- == 0)
                {
                    urn = candidate;
                    return true;
                }
            }

            return false;
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
            if (!catalog.TryGetValue(category, out List<URN>? urns))
            {
                urns = new List<URN>();
                catalog[category] = urns;
            }

            urns.Add(urn);
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
    }
}
