#nullable enable
using System;
using System.Collections.Generic;

namespace Runtime.Wearables
{
    /// <summary>
    /// Emote categories as defined by ADR-74 (EmoteCategory in @dcl/schemas).
    /// </summary>
    public static class EmoteCategories
    {
        public const string DANCE = "dance";
        public const string STUNT = "stunt";
        public const string GREETINGS = "greetings";
        public const string FUN = "fun";
        public const string POSES = "poses";
        public const string REACTIONS = "reactions";
        public const string HORROR = "horror";
        public const string MISCELLANEOUS = "miscellaneous";

        private static readonly HashSet<string> ALL = new (StringComparer.OrdinalIgnoreCase)
        {
            DANCE,
            STUNT,
            GREETINGS,
            FUN,
            POSES,
            REACTIONS,
            HORROR,
            MISCELLANEOUS,
        };

        public static bool IsEmoteCategory(string? category) =>
            !string.IsNullOrEmpty(category) && ALL.Contains(category);
    }
}
