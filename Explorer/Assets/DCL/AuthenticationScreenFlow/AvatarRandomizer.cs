using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DCL.AuthenticationScreenFlow
{
    public sealed class AvatarRandomizer
    {
        private static readonly HashSet<string> FEMALE_EXCLUDED_CATEGORIES = new ()
        {
            WearableCategories.Categories.FACIAL_HAIR,
        };

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

        internal const float OPTIONAL_CATEGORY_INCLUDE_CHANCE = 0.75f;

        private Dictionary<string, List<URN>>? maleCatalog;
        private Dictionary<string, List<URN>>? femaleCatalog;

        private readonly List<URN> selectionBuffer = new (16);

        public bool HasCatalogs => maleCatalog != null;

        internal IReadOnlyDictionary<string, List<URN>>? MaleCatalog => maleCatalog;
        internal IReadOnlyDictionary<string, List<URN>>? FemaleCatalog => femaleCatalog;

        public void PopulateCatalogs(IReadOnlyList<ITrimmedWearable> wearables)
        {
            maleCatalog = new Dictionary<string, List<URN>>();
            femaleCatalog = new Dictionary<string, List<URN>>();

            foreach (ITrimmedWearable wearable in wearables)
            {
                string category = wearable.GetCategory();

                if (category == WearableCategories.Categories.BODY_SHAPE)
                    continue;

                URN urn = wearable.GetUrn();

                if (wearable.IsCompatibleWithBodyShape(BodyShape.MALE)
                    && !HasBodyTypePrefix(urn, "f_"))
                    AddToCatalog(maleCatalog, category, urn);

                if (wearable.IsCompatibleWithBodyShape(BodyShape.FEMALE)
                    && !FEMALE_EXCLUDED_CATEGORIES.Contains(category)
                    && !HasBodyTypePrefix(urn, "m_"))
                    AddToCatalog(femaleCatalog, category, urn);
            }
        }

        internal void AddEntry(string category, URN urn, bool compatWithMale, bool compatWithFemale)
        {
            maleCatalog ??= new Dictionary<string, List<URN>>();
            femaleCatalog ??= new Dictionary<string, List<URN>>();

            if (compatWithMale && !HasBodyTypePrefix(urn, "f_"))
                AddToCatalog(maleCatalog, category, urn);

            if (compatWithFemale
                && !FEMALE_EXCLUDED_CATEGORIES.Contains(category)
                && !HasBodyTypePrefix(urn, "m_"))
                AddToCatalog(femaleCatalog, category, urn);
        }

        public HashSet<URN> SelectRandomWearables(BodyShape bodyShape)
        {
            Dictionary<string, List<URN>> catalog = bodyShape.Equals(BodyShape.MALE)
                ? maleCatalog!
                : femaleCatalog!;

            selectionBuffer.Clear();

            foreach (KeyValuePair<string, List<URN>> kvp in catalog)
            {
                if (kvp.Value.Count == 0)
                    continue;

                if (OPTIONAL_CATEGORIES.Contains(kvp.Key) && Random.value > OPTIONAL_CATEGORY_INCLUDE_CHANCE)
                    continue;

                selectionBuffer.Add(kvp.Value[Random.Range(0, kvp.Value.Count)]);
            }

            return new HashSet<URN>(selectionBuffer);
        }

        public void ClearCatalogs()
        {
            maleCatalog = null;
            femaleCatalog = null;
        }

        public static bool HasBodyTypePrefix(URN urn, string prefix)
        {
            string urnStr = urn.ToString();
            int lastColon = urnStr.LastIndexOf(':');

            if (lastColon < 0 || lastColon >= urnStr.Length - 1)
                return false;

            return urnStr.AsSpan(lastColon + 1).StartsWith(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsOptionalCategory(string category) =>
            OPTIONAL_CATEGORIES.Contains(category);

        internal static bool IsFemaleExcludedCategory(string category) =>
            FEMALE_EXCLUDED_CATEGORIES.Contains(category);

        private static void AddToCatalog(Dictionary<string, List<URN>> catalog, string category, URN urn)
        {
            if (!catalog.TryGetValue(category, out List<URN>? list))
            {
                list = new List<URN>();
                catalog[category] = list;
            }

            list.Add(urn);
        }
    }
}
