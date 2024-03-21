using CommunicationData.URLHelpers;
using Decentraland.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearablesConstants
    {
        public const string EMPTY_DEFAULT_WEARABLE = "EMPTY_DEFAULT_WEARABLE";

        //Used for hiding algorithm
        public static readonly IList<string> CATEGORIES_PRIORITY = new List<string>
        {
            Categories.SKIN,
            Categories.UPPER_BODY,
            Categories.HANDS_WEAR,
            Categories.LOWER_BODY,
            Categories.FEET,
            Categories.HELMET,
            Categories.HAT,
            Categories.TOP_HEAD,
            Categories.MASK,
            Categories.EYEWEAR,
            Categories.EARRING,
            Categories.TIARA,
            Categories.HAIR,
            Categories.EYEBROWS,
            Categories.EYES,
            Categories.MOUTH,
            Categories.FACIAL_HAIR,
            Categories.BODY_SHAPE,
        };

        //Used for hiding algorithm
        public static readonly string[] SKIN_IMPLICIT_CATEGORIES =
        {
            Categories.EYES,
            Categories.MOUTH,
            Categories.EYEBROWS,
            Categories.HAIR,
            Categories.UPPER_BODY,
            Categories.LOWER_BODY,
            Categories.FEET,
            Categories.HANDS,
            Categories.HANDS_WEAR,
            Categories.HEAD,
            Categories.FACIAL_HAIR,
        };

        //Used for hiding algorithm
        public static readonly string[] UPPER_BODY_DEFAULT_HIDES =
        {
            Categories.HANDS,
        };

        public static readonly IReadOnlyList<string> FACIAL_FEATURES = new List<string>
        {
            Categories.EYEBROWS,
            Categories.EYES,
            Categories.MOUTH,
        };

        public static class Categories
        {
            public const string BODY_SHAPE = "body_shape";
            public const string UPPER_BODY = "upper_body";
            public const string LOWER_BODY = "lower_body";
            public const string FEET = "feet";
            public const string EYES = "eyes";
            public const string EYEBROWS = "eyebrows";
            public const string MOUTH = "mouth";
            public const string FACIAL = "facial";
            public const string HAIR = "hair";
            public const string SKIN = "skin";
            public const string FACIAL_HAIR = "facial_hair";
            public const string EYEWEAR = "eyewear";
            public const string TIARA = "tiara";
            public const string EARRING = "earring";
            public const string HAT = "hat";
            public const string TOP_HEAD = "top_head";
            public const string HELMET = "helmet";
            public const string MASK = "mask";
            public const string HANDS = "hands";
            public const string HANDS_WEAR = "hands_wear";
            public const string HEAD = "head";
        }

        public static class DefaultColors
        {
            private static readonly Color[] DEFAULT_SKIN_COLORS
                =
                {
                    new (0.55f, 0.33f, 0.14f),
                    new (0.78f, 0.53f, 0.26f),
                    new (0.88f, 0.67f, 0.41f),
                    new (0.95f, 0.76f, 0.49f),
                    new (1.00f, 0.86f, 0.67f),
                };

            public static Color GetRandomSkinColor() =>
                DEFAULT_SKIN_COLORS[Random.Range(0, DEFAULT_SKIN_COLORS.Length)];

            public static Color GetRandomHairColor() =>
                Random.ColorHSV();

            public static Color GetRandomEyesColor() =>
                Random.ColorHSV();

            public static Color3 GetRandomSkinColor3()
            {
                Color randomColor = GetRandomSkinColor();

                return new Color3
                    { R = randomColor.r, G = randomColor.g, B = randomColor.b };
            }

            public static Color3 GetRandomHairColor3()
            {
                Color randomColor = Random.ColorHSV();

                return new Color3
                    { R = randomColor.r, G = randomColor.g, B = randomColor.b };
            }
        }

        public static class DefaultWearables
        {
            //TODO: Commented wearables that Im not sure we have default ABs
            public static readonly IReadOnlyDictionary<(BodyShape, string), string> DEFAULT_WEARABLES = new Dictionary<(BodyShape, string), string>
            {
                { (BodyShape.MALE, Categories.EYES), "urn:decentraland:off-chain:base-avatars:eyes_00" },
                { (BodyShape.MALE, Categories.EYEBROWS), "urn:decentraland:off-chain:base-avatars:eyebrows_00" },
                { (BodyShape.MALE, Categories.MOUTH), "urn:decentraland:off-chain:base-avatars:mouth_00" },
                { (BodyShape.MALE, Categories.HAIR), "urn:decentraland:off-chain:base-avatars:casual_hair_01" },

                //{ (BodyShapes.MALE, Categories.FACIAL), "urn:decentraland:off-chain:base-avatars:beard" },
                { (BodyShape.MALE, Categories.UPPER_BODY), "urn:decentraland:off-chain:base-avatars:green_hoodie" },
                { (BodyShape.MALE, Categories.LOWER_BODY), "urn:decentraland:off-chain:base-avatars:brown_pants" },
                { (BodyShape.MALE, Categories.FEET), "urn:decentraland:off-chain:base-avatars:sneakers" },

                { (BodyShape.FEMALE, Categories.EYES), "urn:decentraland:off-chain:base-avatars:f_eyes_00" },
                { (BodyShape.FEMALE, Categories.EYEBROWS), "urn:decentraland:off-chain:base-avatars:f_eyebrows_00" },
                { (BodyShape.FEMALE, Categories.MOUTH), "urn:decentraland:off-chain:base-avatars:f_mouth_00" },
                { (BodyShape.FEMALE, Categories.HAIR), "urn:decentraland:off-chain:base-avatars:standard_hair" },
                { (BodyShape.FEMALE, Categories.UPPER_BODY), "urn:decentraland:off-chain:base-avatars:f_sweater" },
                { (BodyShape.FEMALE, Categories.LOWER_BODY), "urn:decentraland:off-chain:base-avatars:f_jeans" },
                { (BodyShape.FEMALE, Categories.FEET), "urn:decentraland:off-chain:base-avatars:bun_shoes" },
            };

            public static HashSet<URN> GetDefaultWearablesForBodyShape(string bodyShapeId) =>
                DEFAULT_WEARABLES.Where(x => x.Key.Item1 == bodyShapeId).Select(x => new URN(x.Value)).ToHashSet();

            public static string GetDefaultWearable(BodyShape bodyShapeId, string category,
                out bool hasEmptyDefaultWearableAB)
            {
                if (!DEFAULT_WEARABLES.ContainsKey((bodyShapeId, category)))
                {
                    hasEmptyDefaultWearableAB = true;
                    return EMPTY_DEFAULT_WEARABLE;
                }

                hasEmptyDefaultWearableAB = false;
                return DEFAULT_WEARABLES[(bodyShapeId, category)];
            }
        }
    }
}
