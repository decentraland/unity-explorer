using System.Collections.Generic;

namespace Runtime.Wearables
{
    public static class WearableCategories
    {
        /// <summary>
        /// Categories priority order for hiding calculation, from highest to lowest.
        /// Higher priority categories hide lower priority ones.
        /// </summary>
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

        public static readonly IReadOnlyList<string> COLOR_PICKER_CATEGORIES = new List<string>
        {
            Categories.EYES,
            Categories.HAIR,
            Categories.BODY_SHAPE,
            Categories.EYEBROWS,
            Categories.FACIAL_HAIR
        };

        public static readonly Dictionary<string, string> READABLE_CATEGORIES = new Dictionary<string, string>()
        {
            {Categories.BODY_SHAPE, "Body shape"},
            {Categories.UPPER_BODY, "Upper body"},
            {Categories.LOWER_BODY, "Lower body"},
            {Categories.FEET, "Feet"},
            {Categories.EYES, "Eyes"},
            {Categories.EYEBROWS, "Eyebrows"},
            {Categories.MOUTH, "Mouth"},
            {Categories.FACIAL, "Facial"},
            {Categories.HAIR, "Hair"},
            {Categories.SKIN, "Skin"},
            {Categories.FACIAL_HAIR, "Facial hair"},
            {Categories.EYEWEAR, "Eyewear"},
            {Categories.TIARA, "Tiara"},
            {Categories.EARRING, "Earring"},
            {Categories.HAT, "Hat"},
            {Categories.TOP_HEAD, "Top head"},
            {Categories.HELMET, "Helmet"},
            {Categories.MASK, "Mask"},
            {Categories.HANDS, "Hands"},
            {Categories.HANDS_WEAR, "Hands wear"},
            {Categories.HEAD, "Head"}
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
    }
}