using DCL.Chat.ChatReactions.Configs;
using DCL.Emoji;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DCL.Chat.ChatMessages
{
    /// <summary>
    /// Builds the display text for reaction tooltips: resolves wallet addresses to
    /// display names, appends "you" for the local user, and adds the emoji shortcode suffix.
    /// Extracted from ReactionTooltipPresenter to keep the presenter focused on lifecycle.
    /// </summary>
    internal sealed class ReactionTooltipTextBuilder
    {
        private readonly IProfileCache profileCache;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly ChatReactionsMessageConfig messageConfig;
        private readonly EmojiMapping emojiMapping;
        private readonly string ownWalletAddress;
        private readonly string actionColorOpenTag;
        private readonly StringBuilder sb = new (256);
        private readonly List<string> unresolvedWallets = new (8);

        public IReadOnlyList<string> UnresolvedWallets => unresolvedWallets;

        public ReactionTooltipTextBuilder(
            IProfileCache profileCache,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionsMessageConfig messageConfig,
            EmojiMapping emojiMapping,
            string ownWalletAddress)
        {
            this.profileCache = profileCache;
            this.atlasConfig = atlasConfig;
            this.messageConfig = messageConfig;
            this.emojiMapping = emojiMapping;
            this.ownWalletAddress = ownWalletAddress;
            actionColorOpenTag = $"<color=#{ColorUtility.ToHtmlStringRGBA(messageConfig.TooltipActionTextColor)}>";
        }

        public string Build(List<string> reactors, int mockUserCount, int emojiIndex, out bool allResolved)
        {
            sb.Clear();
            unresolvedWallets.Clear();
            allResolved = true;

            int count = 0;
            string[] mockNames = messageConfig.TooltipMockUserNames;

            if (mockNames.Length > 0)
            {
                for (int i = 0; i < mockUserCount; i++)
                {
                    if (count > 0)
                        sb.Append(", ");

                    sb.Append(mockNames[UnityEngine.Random.Range(0, mockNames.Length)]);
                    count++;
                }
            }

            bool ownIncluded = false;

            for (int i = 0; i < reactors.Count; i++)
            {
                string wallet = reactors[i];
                bool isOwn = string.Equals(wallet, ownWalletAddress, StringComparison.OrdinalIgnoreCase);
                if (isOwn)
                {
                    ownIncluded = true;
                    continue;
                }

                bool resolved = TryResolveDisplayName(wallet, out string displayName);
                if (!resolved)
                {
                    allResolved = false;
                    unresolvedWallets.Add(wallet);
                }

                if (count > 0)
                    sb.Append(", ");

                sb.Append(displayName);
                count++;
            }

            if (ownIncluded)
            {
                if (count > 0)
                    sb.Append(" and ");

                sb.Append("you");
                count++;
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

            return emojiMapping.ValueMapping.GetValueOrDefault((int)unicode);
        }

        private bool TryResolveDisplayName(string wallet, out string displayName)
        {
            if (profileCache.TryGetCompact(wallet, out Profile.CompactInfo profile))
            {
                string name = profile.DisplayName;
                if (!string.IsNullOrEmpty(name))
                {
                    displayName = name;
                    return true;
                }
            }

            // Fallback: truncated wallet.
            displayName = wallet.Length > 8
                ? string.Concat(wallet[..6], "..", wallet[^4..])
                : wallet;

            return false;
        }
    }
}
