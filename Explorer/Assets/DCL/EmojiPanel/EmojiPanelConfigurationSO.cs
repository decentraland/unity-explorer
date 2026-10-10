using DCL.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.Emoji
{
    [CreateAssetMenu(fileName = "EmojiPanelConfig", menuName = "DCL/Chat/Emoji Panel Config")]
    public class EmojiPanelConfigurationSO : ScriptableObject
    {
        [SerializeField] public List<EmojiSection> EmojiSections;

        [HideInInspector] public ScriptableObject emojiSpriteAsset;
        [HideInInspector] public TextAsset emojiJsonMetadata;

#if UNITY_EDITOR
        public void LoadFromJson()
        {
            if (emojiSpriteAsset == null || emojiJsonMetadata == null)
            {
                ReportHub.LogError(ReportCategory.UNSPECIFIED, "EmojiPanelConfigurationSO: Missing emojiSpriteAsset or emojiJsonMetadata");
                return;
            }

            EmojiGroup[] emojiGroups = JsonConvert.DeserializeObject<EmojiGroup[]>(emojiJsonMetadata.text);
            TMP_SpriteAsset spriteAsset = emojiSpriteAsset as TMP_SpriteAsset;

            if (spriteAsset == null)
            {
                ReportHub.LogError(ReportCategory.UNSPECIFIED, "EmojiPanelConfigurationSO: Could not cast emojiSpriteAsset to TMP_SpriteAsset");
                return;
            }

            EmojiMatchLog log = new EmojiMatchLog();
            FillEmojis(emojiGroups, spriteAsset, log);
            ReportHub.LogProductionInfo(log.ToString());
        }

        private void FillEmojis(EmojiGroup[] emojiGroups, TMP_SpriteAsset spriteAsset, EmojiMatchLog log)
        {
            EmojiSections.Clear();
            log.TotalEmojisInSprites = spriteAsset.spriteCharacterTable.Count;
            //A valid UTF32 value is between 0x000000 and 0x10ffff, inclusive, and should not include surrogate codepoint values (0x00d800 ~ 0x00dfff)
            List<TMP_SpriteCharacter> usableEmojis = spriteAsset.spriteCharacterTable
                                                                .Where(x => x.unicode is <= 0x10ffff and (< 0x00D800 or > 0x00DFFF))
                                                                .ToList();
            HashSet<int> savedEmojis = new ();

            foreach (var group in emojiGroups)
            {
                EmojiSection section = new EmojiSection
                {
                    title = group.group,
                    emojis = new List<SerializableKeyValuePair<string, int>>()
                };

                foreach (var emoji in group.emoji)
                {
                    log.ProcessedEmojis++;

                    string[] baseCodesHex = emoji.@base.Select(x => x.ToString("x4")).ToArray();
                    string primaryHex = baseCodesHex[0];
                    List<(TMP_SpriteCharacter, int)> matches = usableEmojis.Select(x => (x, 0))
                                                                           .Where(x => baseCodesHex.Any(hexCode => x.x.name.Equals(hexCode, StringComparison.InvariantCultureIgnoreCase)))
                                                                           .ToList();

                    foreach (string baseCodeHex in baseCodesHex)
                        for (int i = 0; i < matches.Count; i++)
                            if (matches[i].Item1.name.Equals(baseCodeHex, StringComparison.InvariantCultureIgnoreCase))
                                matches[i] = (matches[i].Item1, matches[i].Item2 + 1);

                    TMP_SpriteCharacter bestMatch = SearchHighestMatch(matches, primaryHex);
                    string shortcode = emoji.shortcodes.Length > 0 ? emoji.shortcodes[0] : "?";
                    string baseCodes = string.Join("+", emoji.@base.Select(x => $"U+{x:X4}"));

                    if (bestMatch == null)
                    {
                        log.DiscardedEmojis++;

                        if (matches.Count == 0)
                        {
                            log.NoMatchCount++;
                            log.AppendDiscard($"NO_GLYPH_MATCH  :{shortcode}: [{baseCodes}] — no sprite name contains any of [{string.Join(", ", baseCodesHex)}]");
                        }
                        else
                        {
                            log.AmbiguousCount++;
                            log.AppendDiscard($"AMBIGUOUS_MATCH :{shortcode}: [{baseCodes}] — {matches.Count} glyphs tied at score {matches[0].Item2}: [{string.Join(", ", matches.Select(m => $"{m.Item1.name}(U+{m.Item1.unicode:X4})"))}]");
                        }

                        continue;
                    }

                    log.MatchedEmojis++;

                    if (emoji.shortcodes.Length > 1)
                        log.MultipleShortcodes++;

                    int unicode = (int)bestMatch.unicode;

                    if (!savedEmojis.Add(unicode))
                    {
                        log.DiscardedEmojis++;
                        log.DuplicateCount++;
                        log.AppendDiscard($"DUPLICATE       :{shortcode}: [{baseCodes}] — glyph U+{unicode:X4} already claimed by an earlier emoji");
                        continue;
                    }

                    section.emojis.Add(new SerializableKeyValuePair<string, int>(emoji.shortcodes[0], unicode));
                }

                EmojiSections.Add(section);
            }
        }

        private TMP_SpriteCharacter SearchHighestMatch(List<(TMP_SpriteCharacter, int)> matches, string primaryHex)
        {
            if (matches.Count == 0)
                return null;
            if (matches.Count == 1)
                return matches[0].Item1;

            matches.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            // If top two glyphs have the same score, break the tie by preferring
            // the glyph whose name matches the first base codepoint (the primary emoji).
            // This handles ZWJ sequences like astronaut (person+rocket) → picks person.
            if (matches[0].Item2 == matches[1].Item2)
            {
                var primary = matches.FirstOrDefault(m =>
                    m.Item1.name.Equals(primaryHex, StringComparison.InvariantCultureIgnoreCase));

                return primary.Item1; // null if no match on primary — still discards
            }

            return matches[0].Item1;
        }

        [Serializable]
        public class Emoji
        {
            public int[] @base;
            public string[] shortcodes;
        }

        [Serializable]
        public class EmojiGroup
        {
            public string group;
            public Emoji[] emoji;
        }

        private class EmojiMatchLog
        {
            public int TotalEmojisInSprites;
            public int ProcessedEmojis;
            public int MatchedEmojis;
            public int DiscardedEmojis;
            public int MultipleShortcodes;
            public int NoMatchCount;
            public int AmbiguousCount;
            public int DuplicateCount;

            private readonly StringBuilder discardDetails = new ();

            public void AppendDiscard(string detail) =>
                discardDetails.AppendLine($"  {detail}");

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine("═══ EMOJI PANEL IMPORT REPORT ═══");
                sb.AppendLine($"Sprite asset glyphs: {TotalEmojisInSprites}");
                sb.AppendLine($"JSON emojis processed: {ProcessedEmojis}");
                sb.AppendLine($"Matched: {MatchedEmojis}");
                sb.AppendLine($"Discarded: {DiscardedEmojis} (no match: {NoMatchCount}, ambiguous: {AmbiguousCount}, duplicate: {DuplicateCount})");
                sb.AppendLine($"Multiple shortcodes: {MultipleShortcodes}");

                if (discardDetails.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("─── DISCARDED EMOJIS ───");
                    sb.Append(discardDetails);
                }

                sb.Append("═════════════════════════════════");
                return sb.ToString();
            }
        }
#endif
    }

    [Serializable]
    public class EmojiSection
    {
        public string title;
        public List<SerializableKeyValuePair<string, int>> emojis = new ();
    }
}
