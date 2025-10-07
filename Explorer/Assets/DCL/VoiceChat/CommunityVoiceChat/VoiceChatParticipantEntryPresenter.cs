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

            view.SetupNameElement(currentParticipantState.Name.Value, nameColor);

            view.ProfilePictureView.SetupAsync(profileRepositoryWrapper, nameColor, currentParticipantState.ProfilePictureUrl, currentParticipantState.WalletId, CancellationToken.None).Forget();


            // We only show context menu button on top of local participant if local participant is a mod.
            var showContextMenuButton = true;

            if (currentParticipantState.Name.Value == localParticipantState.Name.Value)
                showContextMenuButton = VoiceChatRoleHelper.IsModeratorOrOwner(localParticipantState.Role.Value);

            view.SetContextMenuButtonVisibility(showContextMenuButton);


            subscriptionsScope.Add(currentParticipantState.IsMuted.Subscribe(PlayerEntryIsMutedChanged));
            subscriptionsScope.Add(currentParticipantState.IsSpeaking.Subscribe(PlayerEntryIsSpeakingChanged));
            subscriptionsScope.Add(currentParticipantState.IsRequestingToSpeak.Subscribe(PlayerEntryIsRequestingToSpeak));
            subscriptionsScope.Add(currentParticipantState.IsSpeaker.Subscribe(SetUserEntryParent));
            subscriptionsScope.Add(currentParticipantState.IsRequestingToSpeak.Subscribe(SetUserRequestingToSpeak));

            view.ContextMenuButtonClicked += OnContextMenuButtonClicked;
            view.ApproveSpeaker += OnApproveSpeaker;
            view.DenySpeaker += OnDenySpeaker;
            view.OpenPassport += ViewOnOpenPassport;
        }

        private void ViewOnOpenPassport()
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

        private void OnContextMenuButtonClicked(Vector2 position)
        {
            ContextMenuClicked?.Invoke(currentParticipantState, position);
        }

        public void Dispose()
        {
            view.CleanupEntry();
            playerEntriesPool.Release(view);
            subscriptionsScope.Dispose();
        }

        public void ConfigureAsListener()
        {
            view.ConfigureAsListener();
            view.ConfigureTransform(listenersParent, Vector3.one);
        }

        public void ConfigureAsSpeaker()
        {
            view.ConfigureAsSpeaker();
            view.ConfigureTransform(speakersParent, Vector3.one);
        }

        private void SetUserRequestingToSpeak(bool isRequestingToSpeak)
        {
            if (isRequestingToSpeak)
            {
                view.ConfigureTransform(requestToSpeakParent, Vector3.one);

                UserIsRequestingToSpeak?.Invoke(currentParticipantState.Name.Value);
            }
        }

        private void PlayerEntryIsMutedChanged(bool isMuted)
        {
            view.OnIsMutedChanged(isMuted, currentParticipantState.IsSpeaker.Value);
        }

        private void PlayerEntryIsSpeakingChanged(bool isSpeaking)
        {
            if (currentParticipantState.IsMuted.Value) return;

            view.OnChangeIsSpeaking(isSpeaking);
        }

        private void SetUserEntryParent(bool isSpeaker)
        {
            view.IsSpeakingIcon.gameObject.SetActive(isSpeaker);
            var parent = currentParticipantState.IsSpeaker.Value ? speakersParent : listenersParent;
            view.ConfigureTransform(parent , Vector3.one);
        }

        private void PlayerEntryIsRequestingToSpeak(bool isRequestingToSpeak)
        {
            var parent = isRequestingToSpeak ? requestToSpeakParent : listenersParent;
            view.ConfigureTransform(parent, Vector3.one);

            bool showApproveDenySection = isRequestingToSpeak && VoiceChatRoleHelper.IsModeratorOrOwner(localParticipantState.Role.Value);
            view.ShowApproveDenySection(showApproveDenySection);
        }
    }
}
