using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class VoiceChatParticipantEntryPresenter : IDisposable
    {
        public event Action<string>? ApproveSpeaker;
        public event Action<string>? DenySpeaker;
        public event Action<VoiceChatParticipantState , Vector2>? ContextMenuClicked;
        public event Action<string>? UserIsRequestingToSpeak;

        private readonly VoiceChatParticipantEntryView view;
        private readonly IObjectPool<VoiceChatParticipantEntryView> playerEntriesPool;
        private readonly VoiceChatParticipantState currentParticipantState;
        private readonly VoiceChatParticipantState localParticipantState;
        private readonly Transform listenersParent;
        private readonly Transform speakersParent;
        private readonly Transform requestToSpeakParent;
        private readonly EventSubscriptionScope subscriptionsScope = new ();

        private readonly CancellationTokenSource cts = new ();

        public VoiceChatParticipantEntryPresenter(
            VoiceChatParticipantEntryView view,
            VoiceChatParticipantState currentParticipantState,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            IObjectPool<VoiceChatParticipantEntryView> playerEntriesPool,
            Transform listenersParent,
            Transform speakersParent,
            Transform requestToSpeakParent)
        {
            this.view = view;
            this.currentParticipantState = currentParticipantState;
            this.playerEntriesPool = playerEntriesPool;
            this.listenersParent = listenersParent;
            this.speakersParent = speakersParent;
            this.requestToSpeakParent = requestToSpeakParent;
            this.localParticipantState = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState;
            view.gameObject.SetActive(true);
            view.CleanupEntry();

            var nameColor = NameColorHelper.GetNameColor(currentParticipantState.Name.Value);
            view.SetupParticipantProfile(currentParticipantState.Name.Value, nameColor, profileRepositoryWrapper, currentParticipantState.ProfilePictureUrl, currentParticipantState.WalletId, cts.Token);

            // We only show context menu button on top of the local participant if the local participant is a moderator.
            var showContextMenuButton = true;

            if (currentParticipantState.Name.Value == localParticipantState.Name.Value)
                showContextMenuButton = VoiceChatRoleHelper.IsModeratorOrOwner(localParticipantState.Role.Value);

            view.SetContextMenuButtonVisibility(showContextMenuButton);

            subscriptionsScope.Add(currentParticipantState.IsMuted.Subscribe(ParticipantIsMutedChanged));
            subscriptionsScope.Add(currentParticipantState.IsSpeaking.Subscribe(ParticipantIsSpeakingChanged));
            subscriptionsScope.Add(currentParticipantState.IsSpeaker.Subscribe(ParticipantIsSpeakerChanged));
            subscriptionsScope.Add(currentParticipantState.IsRequestingToSpeak.Subscribe(ParticipantRequestingToSpeakChanged));

            view.OpenContextMenu += OnOpenOpenContextMenu;
            view.ApproveSpeaker += OnApproveSpeaker;
            view.DenySpeaker += OnDenySpeaker;
            view.OpenPassport += OnOpenPassport;
        }

        private void OnOpenPassport()
        {
            if (string.IsNullOrEmpty(currentParticipantState.WalletId)) return;

            OpenPassportAsync(currentParticipantState.WalletId, CancellationToken.None).Forget();
            return;

            async UniTask OpenPassportAsync(string userId, CancellationToken ct = default)
            {
                try
                {
                    await ViewDependencies.GlobalUIViews.OpenPassportAsync(userId, ct);
                }
                catch (Exception ex)
                {
                    ReportHub.LogError(ReportCategory.COMMUNITY_VOICE_CHAT, $"Failed to open passport for user {userId}: {ex.Message}");
                }
            }
        }

        private void OnDenySpeaker()
        {
            DenySpeaker?.Invoke(currentParticipantState.WalletId);
        }

        private void OnApproveSpeaker()
        {
            ApproveSpeaker?.Invoke(currentParticipantState.WalletId);
        }

        private void OnOpenOpenContextMenu(Vector2 position)
        {
            ContextMenuClicked?.Invoke(currentParticipantState, position);
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            view.CleanupEntry();
            playerEntriesPool.Release(view);
            subscriptionsScope.Dispose();
            view.OpenContextMenu -= OnOpenOpenContextMenu;
            view.ApproveSpeaker -= OnApproveSpeaker;
            view.DenySpeaker -= OnDenySpeaker;
            view.OpenPassport -= OnOpenPassport;
        }

        public void ConfigureAsListener()
        {
            view.ConfigureInitialState(currentParticipantState.IsSpeaker.Value, currentParticipantState.IsMuted.Value);
            view.SetParent(listenersParent);
        }

        public void ConfigureAsSpeaker()
        {
            view.ConfigureInitialState(currentParticipantState.IsSpeaker.Value, currentParticipantState.IsMuted.Value);
            view.SetParent(speakersParent);
        }

        private void ParticipantRequestingToSpeakChanged(bool isRequestingToSpeak)
        {
            var parent = isRequestingToSpeak ? requestToSpeakParent : listenersParent;
            view.SetParent(parent);

            bool showApproveDenySection = isRequestingToSpeak && VoiceChatRoleHelper.IsModeratorOrOwner(localParticipantState.Role.Value);
            view.ParticipantRequestingToSpeakChanged(showApproveDenySection);

            if (isRequestingToSpeak)
                UserIsRequestingToSpeak?.Invoke(currentParticipantState.Name.Value);
        }

        private void ParticipantIsMutedChanged(bool isMuted)
        {
            if (!currentParticipantState.IsSpeaker.Value) return;

            view.OnIsMutedChanged(isMuted);
        }

        private void ParticipantIsSpeakingChanged(bool isSpeaking)
        {
            if (currentParticipantState.IsMuted.Value) return;

            view.OnIsSpeakingChanged(isSpeaking);
        }

        private void ParticipantIsSpeakerChanged(bool isSpeaker)
        {
            view.OnIsSpeakerChanged(isSpeaker, currentParticipantState.IsMuted.Value);
            var parent = currentParticipantState.IsSpeaker.Value ? speakersParent : listenersParent;
            view.SetParent(parent);
        }
    }
}
