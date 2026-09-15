using System;
using System.Collections.Generic;
using System.Text;

namespace ECS.StreamableLoading.Fonts
{
    public static class FontsourceCatalog
    {
        public const string API_BASE_URL = "https://api.fontsource.org/v1/fonts/";

        private const string PREFERRED_SUBSET = "latin";
        private const string STYLE_NORMAL = "normal";
        private const string STYLE_ITALIC = "italic";

        private const string LATEST_VERSION_SEGMENT = "@latest/";

        private static readonly string[] REGULAR_WEIGHTS = { "400", "500", "300", "600" };
        private static readonly string[] BOLD_WEIGHTS = { "700", "600", "800", "500", "900" };

        public static string? ToFamilyId(string familyName)
        {
            var id = new StringBuilder(familyName.Length);
            var pendingSeparator = false;

            foreach (char c in familyName)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSeparator = id.Length > 0;
                    continue;
                }

                if (!IsAsciiLetterOrDigit(c) && c != '-')
                    return null;

                if (pendingSeparator)
                {
                    id.Append('-');
                    pendingSeparator = false;
                }

                id.Append(char.ToLowerInvariant(c));
            }

            return id.Length > 0 ? id.ToString() : null;
        }

        public static string ApiUrl(string familyId) =>
            API_BASE_URL + familyId;

        public static bool TryGetVariantUrl(FontsourceFamilyRecord record, FontVariant variant, out string url)
        {
            (string[] weights, string style) = variant switch
                                               {
                                                   FontVariant.Regular => (REGULAR_WEIGHTS, STYLE_NORMAL),
                                                   FontVariant.Bold => (BOLD_WEIGHTS, STYLE_NORMAL),
                                                   FontVariant.Italic => (REGULAR_WEIGHTS, STYLE_ITALIC),
                                                   FontVariant.BoldItalic => (BOLD_WEIGHTS, STYLE_ITALIC),
                                                   _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
                                               };

            foreach (string weight in weights)
            {
                if (TryGetTtf(record, weight, style, out url))
                    return true;
            }

            url = string.Empty;
            return false;
        }

        private static bool TryGetTtf(FontsourceFamilyRecord record, string weight, string style, out string url)
        {
            url = string.Empty;

            if (record.variants == null
                || !record.variants.TryGetValue(weight, out Dictionary<string, Dictionary<string, FontsourceFamilyRecord.Files>> styles)
                || !styles.TryGetValue(style, out Dictionary<string, FontsourceFamilyRecord.Files> subsets))
                return false;

            FontsourceFamilyRecord.Files? files = null;

            if (!subsets.TryGetValue(PREFERRED_SUBSET, out files) && record.subsets != null)
                foreach (string subset in record.subsets)
                    if (subsets.TryGetValue(subset, out files))
                        break;

            string? ttf = files?.url?.ttf;

            if (string.IsNullOrEmpty(ttf))
                return false;

            url = string.IsNullOrEmpty(record.npmVersion)
                ? ttf
                : ttf.Replace(LATEST_VERSION_SEGMENT, $"@{record.npmVersion}/");

            return true;
        }

        private static bool IsAsciiLetterOrDigit(char c) =>
            c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
    }
}
