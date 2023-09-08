using DCL.ECSComponents;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public static class WearablesLiterals
    {
        public readonly struct BodyShape : IEquatable<BodyShape>
        {
            public const int COUNT = 2;

            public readonly string Value;
            public readonly int Index;

            private BodyShape(string value, int index)
            {
                Value = value;
                Index = index;
            }

            public static implicit operator string(BodyShape bodyShape) =>
                bodyShape.Value;

            public static implicit operator int(BodyShape bodyShape) =>
                bodyShape.Index;

            public static implicit operator BodyShape(PBAvatarShape pbAvatarShape)
            {
                if (pbAvatarShape.BodyShape == MALE.Value)
                    return MALE;

                if (pbAvatarShape.BodyShape == FEMALE.Value)
                    return FEMALE;

                throw new NotSupportedException($"Body shape {pbAvatarShape.BodyShape} not supported");
            }

            public static readonly BodyShape MALE = new ("urn:decentraland:off-chain:base-avatars:BaseMale", 0);
            public static readonly BodyShape FEMALE = new ("urn:decentraland:off-chain:base-avatars:BaseFemale", 1);

            public bool Equals(BodyShape other) =>
                Value == other.Value && Index == other.Index;

            public override bool Equals(object obj) =>
                obj is BodyShape other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(Value, Index);
        }

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

        public static class DefaultWearables
        {
            //TODO: Commented wearables that Im not sure we have default ABs
            public static readonly IReadOnlyDictionary<(BodyShape, string), string> DEFAULT_WEARABLES = new Dictionary<(BodyShape, string), string>
            {
                //{ (BodyShapes.MALE, Categories.EYES), "urn:decentraland:off-chain:base-avatars:eyes_00" },
                //{ (BodyShapes.MALE, Categories.EYEBROWS), "urn:decentraland:off-chain:base-avatars:eyebrows_00" },
                //{ (BodyShapes.MALE, Categories.MOUTH), "urn:decentraland:off-chain:base-avatars:mouth_00" },
                { (BodyShape.MALE, Categories.HAIR), "urn:decentraland:off-chain:base-avatars:casual_hair_01" },

                //{ (BodyShapes.MALE, Categories.FACIAL), "urn:decentraland:off-chain:base-avatars:beard" },
                { (BodyShape.MALE, Categories.UPPER_BODY), "urn:decentraland:off-chain:base-avatars:green_hoodie" },
                { (BodyShape.MALE, Categories.LOWER_BODY), "urn:decentraland:off-chain:base-avatars:brown_pants" },
                { (BodyShape.MALE, Categories.FEET), "urn:decentraland:off-chain:base-avatars:sneakers" },

                //{ (BodyShapes.FEMALE, Categories.EYES), "urn:decentraland:off-chain:base-avatars:f_eyes_00" },
                //{ (BodyShapes.FEMALE, Categories.EYEBROWS), "urn:decentraland:off-chain:base-avatars:f_eyebrows_00" },
                //{ (BodyShapes.FEMALE, Categories.MOUTH), "urn:decentraland:off-chain:base-avatars:f_mouth_00" },
                { (BodyShape.FEMALE, Categories.HAIR), "urn:decentraland:off-chain:base-avatars:standard_hair" },
                { (BodyShape.FEMALE, Categories.UPPER_BODY), "urn:decentraland:off-chain:base-avatars:f_sweater" },
                { (BodyShape.FEMALE, Categories.LOWER_BODY), "urn:decentraland:off-chain:base-avatars:f_jeans" },
                { (BodyShape.FEMALE, Categories.FEET), "urn:decentraland:off-chain:base-avatars:bun_shoes" },
            };

            public static string[] GetDefaultWearablesForBodyShape(string bodyShapeId) =>
                DEFAULT_WEARABLES.Where(x => x.Key.Item1 == bodyShapeId).Select(x => x.Value).ToArray();

            public static string GetDefaultWearable(BodyShape bodyShapeId, string category)
            {
                if (!DEFAULT_WEARABLES.ContainsKey((bodyShapeId, category)))
                    return null;

                return DEFAULT_WEARABLES[(bodyShapeId, category)];
            }
        }
    }
}
