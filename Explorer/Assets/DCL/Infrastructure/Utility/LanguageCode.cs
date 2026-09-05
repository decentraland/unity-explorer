using System;
using System.Collections.Generic;

namespace DCL.Utilities
{
    public enum LanguageCode
    {
        EN = 0,
        ES = 1,
        FR = 2,
        DE = 3,
        RU = 4,
        PT = 5,
        IT = 6,
        ZH = 7,
        JA = 8,
        KO = 9,
    }

    public static class LanguageCodeParser
    {
        private static readonly Dictionary<string, LanguageCode> Map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // primary language subtags (ISO-639-1)
                ["en"] = LanguageCode.EN, ["es"] = LanguageCode.ES, ["fr"] = LanguageCode.FR, ["de"] = LanguageCode.DE,
                ["ru"] = LanguageCode.RU, ["pt"] = LanguageCode.PT, ["it"] = LanguageCode.IT, ["zh"] = LanguageCode.ZH,
                ["ja"] = LanguageCode.JA, ["ko"] = LanguageCode.KO,

                // common Chinese variants → collapse to ZH
                ["zh-cn"] = LanguageCode.ZH, ["zh-sg"] = LanguageCode.ZH, ["zh-hans"] = LanguageCode.ZH, ["zh-tw"]  = LanguageCode.ZH,
                ["zh-hk"]  = LanguageCode.ZH, ["zh-mo"]  = LanguageCode.ZH, ["zh-hant"] = LanguageCode.ZH,

                // other frequent variants
                ["pt-br"] = LanguageCode.PT, ["pt-pt"] = LanguageCode.PT, ["es-419"] = LanguageCode.ES
            };

        public static LanguageCode Parse(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return LanguageCode.EN;

            // normalize: lower + unify separators
            string norm = code.Trim().Replace('_', '-').ToLowerInvariant();

            // direct map first (zh-hans, pt-br, etc.)
            if (Map.TryGetValue(norm, out var lang))
                return lang;

            // fallback: primary language subtag (e.g., "fr" from "fr-CA")
            int hyphen = norm.IndexOf('-');
            string primary = hyphen >= 0 ? norm[..hyphen] : norm;
            if (Map.TryGetValue(primary, out lang))
                return lang;

            // last chance: exact enum name like "EN"
            if (Enum.TryParse(primary.ToUpperInvariant(), out lang))
                return lang;

            return LanguageCode.EN;
        }
    }
}
