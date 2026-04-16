using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatReactions.Core;
using DCL.Emoji;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    /// <summary>
    /// Builds the display text for reaction tooltips from pre-resolved display names.
    /// Pure text formatter — all profile resolution is handled by the presenter.
    /// </summary>
    internal sealed class ReactionTooltipTextBuilder
    {
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly ChatReactionsMessageConfig messageConfig;
        private readonly EmojiMapping emojiMapping;
        private readonly string actionColorOpenTag;
        private readonly StringBuilder sb = new (256);

        public ReactionTooltipTextBuilder(
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionsMessageConfig messageConfig,
            EmojiMapping emojiMapping)
        {
            this.atlasConfig = atlasConfig;
            this.messageConfig = messageConfig;
            this.emojiMapping = emojiMapping;
            actionColorOpenTag = $"<color=#{ColorUtility.ToHtmlStringRGBA(messageConfig.TooltipActionTextColor)}>";
        }

        public string Build(IReadOnlyList<string> displayNames, bool ownIncluded, int mockUserCount, int emojiIndex)
        {
            sb.Clear();

            int count = 0;
            string[] mockNames = messageConfig.TooltipMockUserNames;

            if (mockNames.Length > 0)
            {
                for (int i = 0; i < mockUserCount; i++)
                {
                    if (count > 0)
                        sb.Append(", ");

                    sb.Append(mockNames[Random.Range(0, mockNames.Length)]);
                    count++;
                }
            }

            for (int i = 0; i < displayNames.Count; i++)
            {
                if (count > 0)
                    sb.Append(", ");

                sb.Append(displayNames[i]);
                count++;
            }

            if (ownIncluded)
            {
                if (count > 0)
                    sb.Append(" and ");

                sb.Append("you");
            }

            AppendActionSuffix(emojiIndex);
            return sb.ToString();
        }

        private void AppendActionSuffix(int emojiIndex)
        {
            string? shortcode = TryGetEmojiShortcode(emojiIndex);

            sb.Append(actionColorOpenTag);

            if (shortcode != null)
                sb.Append(" reacted with ").Append(shortcode);
            else
                sb.Append(" reacted");

            sb.Append("</color>");
        }

        private string? TryGetEmojiShortcode(int emojiIndex)
        {
            uint unicode = atlasConfig.GetUnicodeFromTileIndex(emojiIndex);

            if (unicode == 0) return null;

            return EmojiCodepointHelper.TryGetRegionalIndicatorShortcode(unicode)
                   ?? emojiMapping.ValueMapping.GetValueOrDefault((int)unicode);
        }
    }
}
