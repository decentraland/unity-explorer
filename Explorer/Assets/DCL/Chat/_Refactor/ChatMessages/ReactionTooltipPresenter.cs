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
        private readonly ProfileRepositoryWrapper profileRepository;
        private readonly ChatReactionsAtlasConfig atlasConfig;
        private readonly ChatReactionsMessageConfig messageConfig;
        private readonly ReactionTooltipTextBuilder textBuilder;
        private readonly List<string> lastReactors = new (8);

        private CancellationTokenSource? asyncCts;
        private CancellationTokenSource? delayCts;
        private string? shownMessageId;
        private int shownEmojiIndex = -1;

        public ReactionTooltipPresenter(
            ReactionTooltipView view,
            IProfileCache profileCache,
            ProfileRepositoryWrapper profileRepository,
            ChatReactionsAtlasConfig atlasConfig,
            ChatReactionsMessageConfig messageConfig,
            EmojiMapping emojiMapping,
            string ownWalletAddress)
        {
            this.view = view;
            this.profileRepository = profileRepository;
            this.atlasConfig = atlasConfig;
            this.messageConfig = messageConfig;
            this.textBuilder = new ReactionTooltipTextBuilder(
                profileCache, atlasConfig, messageConfig, emojiMapping, ownWalletAddress);

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

            int mockUserCount = messageConfig.TooltipMockUsersEnabled
                ? messageConfig.TooltipMockUserCount
                : 0;

            if (messageConfig.TooltipMockLoadingEnabled)
            {
                view.ShowLoading(uvRect, atlasConfig.Atlas, pillTransform);
                asyncCts = new CancellationTokenSource();
                MockLoadThenShowAsync(mockUserCount, asyncCts.Token).Forget();
            }
            else
            {
                string text = textBuilder.Build(lastReactors, mockUserCount, emojiIndex, out bool allResolved);
                view.Show(text, uvRect, atlasConfig.Atlas, pillTransform);

                if (!allResolved)
                {
                    asyncCts = new CancellationTokenSource();
                    ResolveAndUpdateAsync(mockUserCount, asyncCts.Token).Forget();
                }
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

        private async UniTaskVoid MockLoadThenShowAsync(int mockUserCount, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(messageConfig.TooltipMockLoadingDelay),
                    cancellationToken: ct);

                if (ct.IsCancellationRequested) return;

                string text = textBuilder.Build(lastReactors, mockUserCount, shownEmojiIndex, out bool allResolved);
                view.UpdateText(text);

                if (!allResolved)
                    await ResolveProfilesAndRebuildTextAsync(mockUserCount, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private async UniTaskVoid ResolveAndUpdateAsync(int mockUserCount, CancellationToken ct)
        {
            try { await ResolveProfilesAndRebuildTextAsync(mockUserCount, ct); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.CHAT_MESSAGES); }
        }

        private async UniTask ResolveProfilesAndRebuildTextAsync(int mockUserCount, CancellationToken ct)
        {
            IReadOnlyList<string> unresolved = textBuilder.UnresolvedWallets;

            for (int i = 0; i < unresolved.Count; i++)
            {
                if (ct.IsCancellationRequested) return;
                await profileRepository.GetProfileAsync(unresolved[i], ct);
            }

            if (ct.IsCancellationRequested) return;

            string updatedText = textBuilder.Build(lastReactors, mockUserCount, shownEmojiIndex, out _);
            view.UpdateText(updatedText);
        }
    }
}
