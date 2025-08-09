using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DCL.Emoji
{
    public class EmojiMapping
    {
        public readonly IReadOnlyDictionary<string, EmojiData> NameMapping;
        public readonly IReadOnlyDictionary<int, string> ValueMapping;

        public EmojiMapping(TextAsset emojiMappingJson, EmojiPanelConfigurationSO emojiPanelConfiguration)
        {
            Dictionary<string, string> deserialized = JsonConvert.DeserializeObject<Dictionary<string, string>>(emojiMappingJson.text);

            var emojiNameMapping = new Dictionary<string, EmojiData>(deserialized.Count);
            var emojiValueMapping = new Dictionary<int, string>(deserialized.Count);

            foreach (KeyValuePair<string, string> emojiData in deserialized)
            {
                if (emojiPanelConfiguration.SpriteAsset.GetSpriteIndexFromName(emojiData.Value.ToUpper()) == -1)
                    continue;

                emojiNameMapping.Add(emojiData.Key, new EmojiData($"\\U000{emojiData.Value.ToUpper()}", emojiData.Key));
                emojiValueMapping.Add(int.Parse(emojiData.Value, NumberStyles.HexNumber), emojiData.Key);
            }

            NameMapping = emojiNameMapping;
            ValueMapping = emojiValueMapping;
        }
    }
}
