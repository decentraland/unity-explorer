using Runtime.Wearables;
using System.Collections.Generic;

namespace DCL.Shop
{
    public static class ShopCategoryTree
    {
        public const string ALL = "all";
        public const string WEARABLE = "wearable";
        public const string EMOTE = "emote";

        public const string EMOTE_DANCE = "dance";
        public const string EMOTE_STUNT = "stunt";
        public const string EMOTE_GREETINGS = "greetings";
        public const string EMOTE_FUN = "fun";
        public const string EMOTE_POSES = "poses";
        public const string EMOTE_REACTIONS = "reactions";
        public const string EMOTE_HORROR = "horror";
        public const string EMOTE_MISCELLANEOUS = "miscellaneous";

        public static readonly IReadOnlyList<string> RARITIES = new[] { "common", "uncommon", "epic", "rare", "legendary", "exotic", "mythic", "unique" };

        public static readonly IReadOnlyDictionary<string, string[]> SUB_CATEGORY_MAP = new Dictionary<string, string[]>
        {
            ["Head"] = new[] { WearableCategories.Categories.HEAD, WearableCategories.Categories.HAIR, WearableCategories.Categories.FACIAL_HAIR, WearableCategories.Categories.EYES, WearableCategories.Categories.EYEBROWS, WearableCategories.Categories.MOUTH },
            ["Facial Hair"] = new[] { WearableCategories.Categories.FACIAL_HAIR },
            ["Hair"] = new[] { WearableCategories.Categories.HAIR },
            ["Eyes"] = new[] { WearableCategories.Categories.EYES },
            ["Eyebrows"] = new[] { WearableCategories.Categories.EYEBROWS },
            ["Mouth"] = new[] { WearableCategories.Categories.MOUTH },
            ["Upper Body"] = new[] { WearableCategories.Categories.UPPER_BODY },
            ["Handwear"] = new[] { WearableCategories.Categories.HANDS_WEAR },
            ["Lower Body"] = new[] { WearableCategories.Categories.LOWER_BODY },
            ["Feet"] = new[] { WearableCategories.Categories.FEET },
            ["Accessories"] = new[] { WearableCategories.Categories.EARRING, WearableCategories.Categories.EYEWEAR, WearableCategories.Categories.HAT, WearableCategories.Categories.HELMET, WearableCategories.Categories.MASK, WearableCategories.Categories.TIARA, WearableCategories.Categories.TOP_HEAD },
            ["Earring"] = new[] { WearableCategories.Categories.EARRING },
            ["Eyewear"] = new[] { WearableCategories.Categories.EYEWEAR },
            ["Hat"] = new[] { WearableCategories.Categories.HAT },
            ["Helmet"] = new[] { WearableCategories.Categories.HELMET },
            ["Mask"] = new[] { WearableCategories.Categories.MASK },
            ["Tiara"] = new[] { WearableCategories.Categories.TIARA },
            ["Top Head"] = new[] { WearableCategories.Categories.TOP_HEAD },
            ["Skins"] = new[] { WearableCategories.Categories.SKIN },
            ["Dance"] = new[] { EMOTE_DANCE },
            ["Stunt"] = new[] { EMOTE_STUNT },
            ["Greetings"] = new[] { EMOTE_GREETINGS },
            ["Fun"] = new[] { EMOTE_FUN },
            ["Poses"] = new[] { EMOTE_POSES },
            ["Reactions"] = new[] { EMOTE_REACTIONS },
            ["Horror"] = new[] { EMOTE_HORROR },
            ["Miscellaneous"] = new[] { EMOTE_MISCELLANEOUS },
        };

        public static readonly IReadOnlyList<Node> TOP = new[]
        {
            new Node(ALL, "Shop All", null, System.Array.Empty<Node>()),
            new Node(WEARABLE, "Wearables", null, new[]
            {
                new Node("Head", "Head", WearableCategories.Categories.HEAD, new[]
                {
                    Leaf("Facial Hair", WearableCategories.Categories.FACIAL_HAIR),
                    Leaf("Hair", WearableCategories.Categories.HAIR),
                    Leaf("Eyes", WearableCategories.Categories.EYES),
                    Leaf("Eyebrows", WearableCategories.Categories.EYEBROWS),
                    Leaf("Mouth", WearableCategories.Categories.MOUTH),
                }),
                Leaf("Upper Body", WearableCategories.Categories.UPPER_BODY),
                Leaf("Handwear", WearableCategories.Categories.HANDS_WEAR),
                Leaf("Lower Body", WearableCategories.Categories.LOWER_BODY),
                Leaf("Feet", WearableCategories.Categories.FEET),
                new Node("Accessories", "Accessories", WearableCategories.Categories.EYEWEAR, new[]
                {
                    Leaf("Earring", WearableCategories.Categories.EARRING),
                    Leaf("Eyewear", WearableCategories.Categories.EYEWEAR),
                    Leaf("Hat", WearableCategories.Categories.HAT),
                    Leaf("Helmet", WearableCategories.Categories.HELMET),
                    Leaf("Mask", WearableCategories.Categories.MASK),
                    Leaf("Tiara", WearableCategories.Categories.TIARA),
                    Leaf("Top Head", WearableCategories.Categories.TOP_HEAD),
                }),
                Leaf("Skins", WearableCategories.Categories.SKIN),
            }),
            new Node(EMOTE, "Emotes", null, new[]
            {
                Leaf("Dance", EMOTE_DANCE),
                Leaf("Stunt", EMOTE_STUNT),
                Leaf("Greetings", EMOTE_GREETINGS),
                Leaf("Fun", EMOTE_FUN),
                Leaf("Poses", EMOTE_POSES),
                Leaf("Reactions", EMOTE_REACTIONS),
                Leaf("Horror", EMOTE_HORROR),
                Leaf("Miscellaneous", EMOTE_MISCELLANEOUS),
            }),
        };

        public static bool TryGetSubCategoryLabel(string key, out string label)
        {
            foreach (Node top in TOP)
            {
                if (TryFind(top.Children, key, out Node? node))
                {
                    label = node!.Label;
                    return true;
                }
            }

            label = string.Empty;
            return false;
        }

        private static Node Leaf(string key, string category) =>
            new (key, key, category, System.Array.Empty<Node>());

        private static bool TryFind(IReadOnlyList<Node> nodes, string key, out Node? found)
        {
            foreach (Node node in nodes)
            {
                if (node.Key == key)
                {
                    found = node;
                    return true;
                }

                if (TryFind(node.Children, key, out found))
                    return true;
            }

            found = null;
            return false;
        }

        public sealed class Node
        {
            public readonly string Key;
            public readonly string Label;
            public readonly string? IconCategory;
            public readonly IReadOnlyList<Node> Children;

            public bool HasChildren => Children.Count > 0;

            internal Node(string key, string label, string? iconCategory, IReadOnlyList<Node> children)
            {
                Key = key;
                Label = label;
                IconCategory = iconCategory;
                Children = children;
            }
        }
    }
}
