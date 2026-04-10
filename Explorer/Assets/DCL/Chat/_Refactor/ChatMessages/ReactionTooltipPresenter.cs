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

#if UNITY_EDITOR
            int mockUserCount = messageConfig.TooltipMockUsersEnabled
                ? messageConfig.TooltipMockUserCount
                : 0;

            if (messageConfig.TooltipMockLoadingEnabled)
            {
                view.ShowLoading(uvRect, atlasConfig.Atlas, pillTransform);
                asyncCts = new CancellationTokenSource();
                MockLoadThenShowAsync(mockUserCount, asyncCts.Token).Forget();
                return;
            }
#else
            const int mockUserCount = 0;
#endif

            bool allResolved = ResolveDisplayNamesFromCache(out bool ownIncluded);
            string text = textBuilder.Build(displayNames, ownIncluded, mockUserCount, emojiIndex);
            view.Show(text, uvRect, atlasConfig.Atlas, pillTransform);

            if (!allResolved)
            {
                asyncCts = new CancellationTokenSource();
                ResolveAndUpdateAsync(mockUserCount, asyncCts.Token).Forget();
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

#if UNITY_EDITOR
        private async UniTaskVoid MockLoadThenShowAsync(int mockUserCount, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(messageConfig.TooltipMockLoadingDelay),
                    cancellationToken: ct);

                if (ct.IsCancellationRequested) return;

                bool allResolved = ResolveDisplayNamesFromCache(out bool ownIncluded);
                string text = textBuilder.Build(displayNames, ownIncluded, mockUserCount, shownEmojiIndex);
                view.UpdateText(text);

                if (!allResolved)
                    await ResolveProfilesAndRebuildTextAsync(mockUserCount, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }
#endif

        private async UniTaskVoid ResolveAndUpdateAsync(int mockUserCount, CancellationToken ct)
        {
            try { await ResolveProfilesAndRebuildTextAsync(mockUserCount, ct); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private async UniTask ResolveProfilesAndRebuildTextAsync(int mockUserCount, CancellationToken ct)
        {
            // Populates the profile cache as a side effect; return value intentionally discarded
            await profileRepository.GetProfilesAsync(unresolvedWallets, ct);

            if (ct.IsCancellationRequested) return;

            ResolveDisplayNamesFromCache(out bool ownIncluded);
            string updatedText = textBuilder.Build(displayNames, ownIncluded, mockUserCount, shownEmojiIndex);
            view.UpdateText(updatedText);
        }

        private bool ResolveDisplayNamesFromCache(out bool ownIncluded)
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

                if (profileCache.TryGetCompact(wallet, out Profile.CompactInfo profile)
                    && profile.DisplayName.Length > 0)
                {
                    displayNames.Add(profile.DisplayName);
                }
                else
                {
                    string fallback = wallet.Length > 8
                        ? string.Concat(wallet[..6], "...", wallet[^4..])
                        : wallet;
                    displayNames.Add(fallback);
                    unresolvedWallets.Add(wallet);
                    allResolved = false;
                }
            }

            return allResolved;
        }
    }
}
