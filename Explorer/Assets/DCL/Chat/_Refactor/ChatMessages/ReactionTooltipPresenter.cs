using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Chat.ChatMessages
{
    public class ReactionTooltipPresenter : IDisposable
    {
        private readonly ReactionTooltipView view;
        private readonly IProfileCache profileCache;
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly string ownWalletAddress;
        private readonly StringBuilder sb = new (256);
        private readonly List<string> unresolvedWallets = new (8);
        private readonly List<string> lastReactors = new (8);

        private CancellationTokenSource? asyncCts;
        private string? shownMessageId;

        public ReactionTooltipPresenter(
            ReactionTooltipView view,
            IProfileCache profileCache,
            ProfileRepositoryWrapper profileRepository,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionsMessageConfig messageConfig,
            string ownWalletAddress)
        {
            this.view = view;
            this.profileCache = profileCache;
            this.profileRepository = profileRepository;
            this.atlasConfig = atlasConfig;
            this.ownWalletAddress = ownWalletAddress;

            var positioner = new ReactionTooltipPositioner(
                (RectTransform)view.transform,
                view.ArrowTransform,
                view.CenteringReference,
                messageConfig.TooltipConfig);

            view.Initialize(positioner);
        }

        public void ShowForReaction(ReactionSet? reactions, int emojiIndex, RectTransform pillTransform, string messageId)
        {
            asyncCts.SafeCancelAndDispose();

            if (reactions == null)
            {
                Hide();
                return;
            }

            IReadOnlyCollection<string>? reactors = reactions.GetReactors(emojiIndex);
            if (reactors == null || reactors.Count == 0)
            {
                Hide();
                return;
            }

            shownMessageId = messageId;

            Rect uvRect = atlasConfig.GetUVRect(emojiIndex);

            lastReactors.Clear();
            foreach (string wallet in reactors)
                lastReactors.Add(wallet);

            unresolvedWallets.Clear();
            string text = BuildText(lastReactors, out bool allResolved);

            view.Show(text, uvRect, atlasConfig.Atlas, pillTransform);

            if (!allResolved)
            {
                asyncCts = new CancellationTokenSource();
                ResolveAndUpdateAsync(asyncCts.Token).Forget();
            }
        }

        public void Hide()
        {
            asyncCts.SafeCancelAndDispose();
            asyncCts = null;
            shownMessageId = null;
            view.Hide();
        }

        public void HideIfShowingMessage(string messageId)
        {
            if (string.Equals(shownMessageId, messageId, StringComparison.Ordinal))
                Hide();
        }

        public void Dispose()
        {
            asyncCts.SafeCancelAndDispose();
            asyncCts = null;
        }

        private string BuildText(List<string> reactors, out bool allResolved)
        {
            sb.Clear();
            unresolvedWallets.Clear();
            allResolved = true;

            bool ownIncluded = false;
            int count = 0;

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

            sb.Append(" reacted");
            return sb.ToString();
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

            // Fallback: truncated wallet
            displayName = wallet.Length > 8
                ? string.Concat(wallet[..6], "..", wallet[^4..])
                : wallet;

            return false;
        }

        private async UniTaskVoid ResolveAndUpdateAsync(CancellationToken ct)
        {
            try
            {
                for (int i = 0; i < unresolvedWallets.Count; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    await profileRepository.GetProfileAsync(unresolvedWallets[i], ct);
                }

                if (ct.IsCancellationRequested) return;

                // Rebuild text now that profiles are cached
                string updatedText = BuildText(lastReactors, out _);
                view.UpdateText(updatedText);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }
    }
}
