using DCL.Chat.ChatReactions.Core;
using System.Collections.Generic;
using System.Linq;

namespace DCL.Emoji
{
    public class EmojiMapping
    {
        public readonly IReadOnlyDictionary<string, EmojiData> NameMapping;
        public readonly IReadOnlyDictionary<int, string> ValueMapping;

        public EmojiMapping(EmojiPanelConfigurationSO emojiPanelConfiguration)
        {
            var emojiNameMapping = new Dictionary<string, EmojiData>();
            var emojiValueMapping = new Dictionary<int, string>();

            foreach (var kvp in emojiPanelConfiguration.EmojiSections.SelectMany(section => section.emojis))
            {
                emojiNameMapping.Add(kvp.key, new EmojiData(char.ConvertFromUtf32(kvp.value), kvp.key));
                emojiValueMapping.Add(kvp.value, kvp.key);
            }

            // Regional indicator symbols (U+1F1E6–U+1F1FF) render as boxed letters A–Z.
            // The panel config maps them to flag shortcodes (flags are pairs of these),
            // so standalone letter shortcodes don't exist. Add them to NameMapping so
            // typing :letter-A: through :letter-Z: in chat resolves correctly.
            for (uint i = 0; i < EmojiCodepointHelper.REGIONAL_INDICATOR_COUNT; i++)
            {
                uint unicode = EmojiCodepointHelper.REGIONAL_INDICATOR_START + i;
                string shortcode = EmojiCodepointHelper.TryGetRegionalIndicatorShortcode(unicode)!;

                if (!emojiNameMapping.ContainsKey(shortcode))
                    emojiNameMapping.Add(shortcode, new EmojiData(char.ConvertFromUtf32((int)unicode), shortcode));
            }

            NameMapping = emojiNameMapping;
            ValueMapping = emojiValueMapping;
        }
    }
}
