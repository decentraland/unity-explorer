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

            NameMapping = emojiNameMapping;
            ValueMapping = emojiValueMapping;
        }
    }
}
