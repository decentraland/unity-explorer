#nullable enable

using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using Decentraland.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearablesConstants
    {
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

            public static Color3 GetRandomEyesColor3()
            {
                Color randomColor = GetRandomEyesColor();

                return new Color3
                    { R = randomColor.r, G = randomColor.g, B = randomColor.b };
            }
        }

        public static class DefaultWearables
        {
            //TODO: Commented wearables that Im not sure we have default ABs
            public static readonly IReadOnlyDictionary<(BodyShape, string), string> DEFAULT_WEARABLES = new Dictionary<(BodyShape, string), string>
            {
                { (BodyShape.MALE, WearableCategories.Categories.EYES), "urn:decentraland:off-chain:base-avatars:eyes_00" },
                { (BodyShape.MALE, WearableCategories.Categories.EYEBROWS), "urn:decentraland:off-chain:base-avatars:eyebrows_00" },
                { (BodyShape.MALE, WearableCategories.Categories.MOUTH), "urn:decentraland:off-chain:base-avatars:mouth_00" },
                { (BodyShape.MALE, WearableCategories.Categories.HAIR), "urn:decentraland:off-chain:base-avatars:casual_hair_01" },

                //{ (BodyShapes.MALE, Categories.FACIAL), "urn:decentraland:off-chain:base-avatars:beard" },
                { (BodyShape.MALE, WearableCategories.Categories.UPPER_BODY), "urn:decentraland:off-chain:base-avatars:green_hoodie" },
                { (BodyShape.MALE, WearableCategories.Categories.LOWER_BODY), "urn:decentraland:off-chain:base-avatars:brown_pants" },
                { (BodyShape.MALE, WearableCategories.Categories.FEET), "urn:decentraland:off-chain:base-avatars:sneakers" },

                { (BodyShape.FEMALE, WearableCategories.Categories.EYES), "urn:decentraland:off-chain:base-avatars:f_eyes_00" },
                { (BodyShape.FEMALE, WearableCategories.Categories.EYEBROWS), "urn:decentraland:off-chain:base-avatars:f_eyebrows_00" },
                { (BodyShape.FEMALE, WearableCategories.Categories.MOUTH), "urn:decentraland:off-chain:base-avatars:f_mouth_00" },
                { (BodyShape.FEMALE, WearableCategories.Categories.HAIR), "urn:decentraland:off-chain:base-avatars:standard_hair" },
                { (BodyShape.FEMALE, WearableCategories.Categories.UPPER_BODY), "urn:decentraland:off-chain:base-avatars:f_sweater" },
                { (BodyShape.FEMALE, WearableCategories.Categories.LOWER_BODY), "urn:decentraland:off-chain:base-avatars:f_jeans" },
                { (BodyShape.FEMALE, WearableCategories.Categories.FEET), "urn:decentraland:off-chain:base-avatars:bun_shoes" },
            };

            public static HashSet<URN> GetDefaultWearablesForBodyShape(string bodyShapeId) =>
                DEFAULT_WEARABLES.Where(x => x.Key.Item1 == bodyShapeId).Select(x => new URN(x.Value!)).ToHashSet();
        }
    }
}
