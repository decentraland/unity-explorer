using System.Collections.Generic;
using System.Globalization;

namespace DCL.Chat.ChatServices.TranslationService.Utilities
{
    public static class EmojiDetector
    {
        public readonly struct EmojiRun
        {
            public readonly int Index;     // UTF-16 index in the source string
            public readonly string Value;  // The emoji grapheme

            public EmojiRun(int index, string value)
            {
                Index = index;
                Value = value;
            }

            public override string ToString()
            {
                return $"{Value} @{Index}";
            }
        }

        /// <summary>
        ///     Returns all emoji graphemes found in 'text'.
        /// </summary>
        public static List<EmojiRun> FindEmoji(string text)
        {
            var outList = new List<EmojiRun>();
            if (string.IsNullOrEmpty(text)) return outList;

            var e = StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext())
            {
                string g = e.GetTextElement();
                int idx = e.ElementIndex; // start index of the grapheme in 'text'
                if (IsEmojiGrapheme(g))
                    outList.Add(new EmojiRun(idx, g));
            }

            return outList;
        }

        /// <summary>
        ///     True if the grapheme is likely an emoji (covers ZWJ sequences, flags, keycaps, modifiers).
        /// </summary>
        public static bool IsEmojiGrapheme(string g)
        {
            // Fast signals
            if (g.IndexOf('\u200D') >= 0) return true; // ZWJ: 👨‍👩‍👧 etc.
            if (g.IndexOf('\uFE0F') >= 0) return true; // VS-16 (emoji presentation)
            if (g.IndexOf('\u20E3') >= 0) return true; // keycap ◌⃣

            int riCount = 0; // regional indicator count (flags)
            bool hasEmojiScalar = false;
            bool hasToneMod = false;
            bool hasTag = false;

            for (int i = 0; i < g.Length;)
            {
                int cp = char.ConvertToUtf32(g, i);
                i += char.IsSurrogatePair(g, i) ? 2 : 1;

                if (IsEmojiScalar(cp)) hasEmojiScalar = true;
                if (cp >= 0x1F1E6 && cp <= 0x1F1FF) riCount++;                   // Regional indicators
                if (cp >= 0x1F3FB && cp <= 0x1F3FF) hasToneMod = true;           // Skin tone
                if (cp >= 0xE0020 && cp <= 0xE007F) hasTag = true;               // Tag sequences (subdivision flags)
            }

            if (riCount >= 2) return true;       // a flag is two RI scalars
            if (hasTag) return true;             // tagged emoji sequences
            if (hasToneMod) return true;         // emoji + skin tone
            return hasEmojiScalar;               // normal emoji blocks / legacy symbols
        }

        private static bool IsEmojiScalar(int cp)
        {
            return
                // Core emoji ranges
                (cp >= 0x1F300 && cp <= 0x1F5FF) || // Misc Symbols & Pictographs
                (cp >= 0x1F600 && cp <= 0x1F64F) || // Emoticons
                (cp >= 0x1F680 && cp <= 0x1F6FF) || // Transport & Map
                (cp >= 0x1F700 && cp <= 0x1F77F) || // Alchemical Symbols (some rendered emoji)
                (cp >= 0x1F780 && cp <= 0x1F7FF) || // Geometric Shapes Ext
                (cp >= 0x1F800 && cp <= 0x1F8FF) || // Supplemental Arrows-C (rare)
                (cp >= 0x1F900 && cp <= 0x1F9FF) || // Supplemental Symbols & Pictographs
                (cp >= 0x1FA70 && cp <= 0x1FAFF) || // Symbols & Pictographs Ext-A
                // Common legacy “emoji-presentable” blocks
                (cp >= 0x2600  && cp <= 0x26FF)  || // Misc Symbols (☀️, ☔, ♻️…)
                (cp >= 0x2700  && cp <= 0x27BF)  || // Dingbats (✈️, ✉️…)
                // Singletons often seen as emoji
                cp == 0x00A9 || cp == 0x00AE || cp == 0x3030 || cp == 0x303D || cp == 0x3297 || cp == 0x3299;
        }
    }
}