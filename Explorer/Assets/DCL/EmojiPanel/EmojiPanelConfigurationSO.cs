using DCL.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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

                    string firstHexCode = emoji.@base[0].ToString("X");
                    List<(TMP_SpriteCharacter, int)> matches = usableEmojis.Select(x => (x, 0))
                                                                           .Where(x => x.x.name.Contains(firstHexCode, StringComparison.InvariantCultureIgnoreCase))
                                                                           .ToList();

                    foreach (int baseCode in emoji.@base)
                    {
                        string hexCode = baseCode.ToString("X");

                        for (int i = 0; i < matches.Count; i++)
                            if (matches[i].Item1.name.Contains(hexCode, StringComparison.InvariantCultureIgnoreCase))
                                matches[i] = (matches[i].Item1, matches[i].Item2 + 1);
                    }

                    TMP_SpriteCharacter bestMatch = SearchHighestMatch(matches);

                    if (bestMatch == null)
                    {
                        log.DiscardedEmojis++;
                        continue;
                    }

                    log.MatchedEmojis++;

                    if (emoji.shortcodes.Length > 1)
                        log.MultipleShortcodes++;

                    section.emojis.Add(new SerializableKeyValuePair<string, int>(emoji.shortcodes[0], (int)bestMatch.unicode));
                }

                EmojiSections.Add(section);
            }
        }

        private TMP_SpriteCharacter SearchHighestMatch(List<(TMP_SpriteCharacter, int)> matches)
        {
            if (matches.Count == 0)
                return null;
            if (matches.Count == 1)
                return matches[0].Item1;

            matches.Sort((a, b) => b.Item2.CompareTo(a.Item2));

            //If we have two glyphs with the same match count, we cannot decide which one to use, so we discard the emoji
            if (matches[0].Item2 == matches[1].Item2)
                return null;

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

            public override string ToString() =>
                $"Emojis in sprite asset: {TotalEmojisInSprites}\nProcessed emojis: {ProcessedEmojis}\nMatched emojis: {MatchedEmojis}\nDiscarded emojis: {DiscardedEmojis}\nEmojis with multiple shortcodes: {MultipleShortcodes}";
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
