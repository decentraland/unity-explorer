using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Emoji;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using System;
using System.Collections.Generic;
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
        private readonly ChatReactionsMessageConfig messageConfig;
        private readonly ReactionTooltipTextBuilder textBuilder;
        private readonly string ownWalletAddress;
        private readonly List<string> lastReactors = new (8);
        private readonly List<string> displayNames = new (8);
        private readonly List<string> unresolvedWallets = new (8);

        private CancellationTokenSource? asyncCts;
        private CancellationTokenSource? delayCts;
        private string? shownMessageId;
        private int shownEmojiIndex = -1;

        internal ReactionTooltipPresenter(
            ReactionTooltipView view,
            IProfileCache profileCache,
            ProfileRepositoryWrapper profileRepository,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionsMessageConfig messageConfig,
            EmojiMapping emojiMapping,
            string ownWalletAddress)
        {
            this.view = view;
            this.profileCache = profileCache;
            this.profileRepository = profileRepository;
            this.atlasConfig = atlasConfig;
            this.messageConfig = messageConfig;
            this.ownWalletAddress = ownWalletAddress;
            this.textBuilder = new ReactionTooltipTextBuilder(atlasConfig, messageConfig, emojiMapping);

            var positioner = new ReactionTooltipPositioner(
                (RectTransform)view.transform,
                view.ArrowTransform,
                view.CenteringReference,
                messageConfig.TooltipConfig);

            view.Initialize(positioner);
        }

        public void ShowForReaction(ReactionSet? reactions, int emojiIndex, RectTransform pillTransform, string messageId)
        {
            if (IsAlreadyShowing(messageId, emojiIndex))
                return;

            delayCts.SafeCancelAndDispose();
            delayCts = null;
            asyncCts.SafeCancelAndDispose();
            asyncCts = null;

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

            float hoverDelay = messageConfig.TooltipHoverDelay;

            if (hoverDelay > 0f)
            {
                delayCts = new CancellationTokenSource();
                DelayedShowAsync(reactions, emojiIndex, pillTransform, messageId, hoverDelay, delayCts.Token).Forget();
            }
            else
            {
                ShowTooltipImmediate(reactions, emojiIndex, pillTransform, messageId);
            }
        }

        public void Hide()
        {
            delayCts.SafeCancelAndDispose();
            delayCts = null;
            asyncCts.SafeCancelAndDispose();
            asyncCts = null;
            shownMessageId = null;
            shownEmojiIndex = -1;
            view.Hide();
        }

        public void Dispose()
        {
            delayCts.SafeCancelAndDispose();
            delayCts = null;
            asyncCts.SafeCancelAndDispose();
            asyncCts = null;
        }

        private void ShowTooltipImmediate(ReactionSet reactions, int emojiIndex, RectTransform pillTransform, string messageId)
        {
            shownMessageId = messageId;
            shownEmojiIndex = emojiIndex;

            Rect uvRect = atlasConfig.GetUVRect(emojiIndex);

            lastReactors.Clear();
            IReadOnlyCollection<string>? reactors = reactions.GetReactors(emojiIndex);
            if (reactors != null)
            {
                foreach (string wallet in reactors)
                    lastReactors.Add(wallet);
            }

            bool allResolved = ResolveDisplayNames(out bool ownIncluded);
            string text = textBuilder.Build(displayNames, ownIncluded, emojiIndex);
            view.Show(text, uvRect, atlasConfig.Atlas, pillTransform);

            if (!allResolved)
            {
                asyncCts = new CancellationTokenSource();
                ResolveAndUpdateAsync(asyncCts.Token).Forget();
            }
        }

        private async UniTaskVoid DelayedShowAsync(ReactionSet reactions, int emojiIndex, RectTransform pillTransform, string messageId, float delay, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);

                if (ct.IsCancellationRequested) return;

                ShowTooltipImmediate(reactions, emojiIndex, pillTransform, messageId);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private bool IsAlreadyShowing(string messageId, int emojiIndex) =>
            shownEmojiIndex == emojiIndex
            && string.Equals(shownMessageId, messageId, StringComparison.Ordinal);

        private async UniTaskVoid ResolveAndUpdateAsync(CancellationToken ct)
        {
            try
            {
                List<Profile.CompactInfo> fetched = await profileRepository.GetProfilesAsync(unresolvedWallets, ct);

                if (ct.IsCancellationRequested) return;

                ResolveDisplayNames(out bool ownIncluded, fetched);
                string updatedText = textBuilder.Build(displayNames, ownIncluded, shownEmojiIndex);
                view.UpdateText(updatedText);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private bool ResolveDisplayNames(out bool ownIncluded, IReadOnlyList<Profile.CompactInfo>? fetched = null)
        {
            displayNames.Clear();
            unresolvedWallets.Clear();
            ownIncluded = false;
            bool allResolved = true;

            for (int i = 0; i < lastReactors.Count; i++)
            {
                string wallet = lastReactors[i];

                if (string.Equals(wallet, ownWalletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    ownIncluded = true;
                    continue;
                }

                // A cached or just-fetched profile means we've already asked the repository.
                // Even if its DisplayName is empty, do not re-queue — the next fetch would return the same thing.
                if (TryGetProfile(fetched, wallet, out Profile.CompactInfo profile))
                {
                    displayNames.Add(profile.DisplayName.Length > 0
                        ? profile.DisplayName
                        : ShortenWallet(wallet));
                    continue;
                }

                displayNames.Add(ShortenWallet(wallet));
                unresolvedWallets.Add(wallet);
                allResolved = false;
            }

            return allResolved;
        }

        private bool TryGetProfile(
            IReadOnlyList<Profile.CompactInfo>? fetched, string wallet, out Profile.CompactInfo profile)
        {
            if (fetched != null)
            {
                for (int i = 0; i < fetched.Count; i++)
                {
                    Profile.CompactInfo candidate = fetched[i];
                    if (string.Equals(candidate.UserId, wallet, StringComparison.OrdinalIgnoreCase))
                    {
                        profile = candidate;
                        return true;
                    }
                }
            }

            return profileCache.TryGetCompact(wallet, out profile);
        }

        private static string ShortenWallet(string wallet) =>
            wallet.Length > 8 ? string.Concat(wallet[..6], "...", wallet[^4..]) : wallet;
    }
}
