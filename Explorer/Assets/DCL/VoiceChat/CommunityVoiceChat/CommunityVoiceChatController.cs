using Cysharp.Threading.Tasks;
using DCL.Profiles.Helpers;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatController : IDisposable
    {
        private readonly CommunityVoiceChatTitlebarView view;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly VoiceChatRoomManager roomManager;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IObjectPool<PlayerEntryView> playerEntriesPool;
        private readonly Dictionary<string, PlayerEntryView> usedPlayerEntries = new ();
        private readonly CommunityVoiceChatSearchController communityVoiceChatSearchController;
        private readonly CommunityVoiceChatInCallController inCallController;
        private readonly IDisposable? voiceChatTypeSubscription;

        private bool isPanelCollapsed;

        public CommunityVoiceChatController(
            CommunityVoiceChatTitlebarView view,
            PlayerEntryView playerEntry,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            VoiceChatRoomManager roomManager)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.roomManager = roomManager;

            communityVoiceChatSearchController = new CommunityVoiceChatSearchController(view.CommunityVoiceChatSearchView);
            inCallController = new CommunityVoiceChatInCallController(view.CommunityVoiceChatInCallView, voiceChatOrchestrator, microphoneHandler);

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed += OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined += OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft += OnParticipantLeft;

            this.view.CollapseButtonClicked += OnCollapsedButtonClicked;
            this.roomManager.ConnectionEstablished += OnConnectionEnstablished;
            this.view.ApproveSpeaker += PromoteToSpeaker;
            this.view.DenySpeaker += DenySpeaker;

            // Should we send this through an internal event bus to avoid having these sub-view subscriptions or bubbling up events?
            view.CommunityVoiceChatInCallView.InCallFooterView.OpenListenersSectionButton.onClick.AddListener(OpenListenersSection);
            view.CommunityVoiceChatSearchView.BackButton.onClick.AddListener(CloseListenersSection);

            playerEntriesPool = new ObjectPool<PlayerEntryView>(
                () => Object.Instantiate(playerEntry),
                actionOnGet: entry => entry.gameObject.SetActive(true),
                actionOnRelease: entry => entry.gameObject.SetActive(false));

            voiceChatTypeSubscription = voiceChatOrchestrator.CurrentVoiceChatType.Subscribe(OnVoiceChatTypeChanged);

            OnVoiceChatTypeChanged(voiceChatOrchestrator.CurrentVoiceChatType.Value);

            //Temporary fix, this will be moved to the Show function to set expanded as default state
            voiceChatOrchestrator.ChangePanelSize(VoiceChatPanelSize.EXPANDED);
        }

        private void PromoteToSpeaker(string walletId)
        {
            voiceChatOrchestrator.PromoteToSpeakerInCurrentCall(walletId);
        }

        private void DenySpeaker(string walletId)
        {
            voiceChatOrchestrator.DenySpeakerInCurrentCall(walletId);
        }

        private void OnConnectionEnstablished()
        {
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value == VoiceChatType.COMMUNITY)
                view.SetConnectedPanel(true);
        }

        public void Dispose()
        {
            roomManager.ConnectionEstablished -= OnConnectionEnstablished;

            view.CollapseButtonClicked -= OnCollapsedButtonClicked;
            view.ApproveSpeaker -= PromoteToSpeaker;
            view.DenySpeaker -= DenySpeaker;

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed -= OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined -= OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft -= OnParticipantLeft;

            voiceChatTypeSubscription?.Dispose();
            communityVoiceChatSearchController?.Dispose();

            ClearPool();
        }

        private void CloseListenersSection()
        {
            view.CommunityVoiceChatSearchView.gameObject.SetActive(false);
            view.CommunityVoiceChatInCallView.gameObject.SetActive(true);
        }

        private void OpenListenersSection()
        {
            view.CommunityVoiceChatSearchView.gameObject.SetActive(true);
            view.CommunityVoiceChatInCallView.gameObject.SetActive(false);
        }

        private void OnParticipantLeft(string participantId)
        {
            RemoveParticipant(participantId);
            inCallController.SetParticipantCount(voiceChatOrchestrator.ParticipantsStateService.ConnectedParticipants.Count);
        }

        private void OnParticipantJoined(string participantId, VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            if (participantState.IsSpeaker)
                AddSpeaker(participantState);
            else
                AddListener(participantState);

            inCallController.SetParticipantCount(voiceChatOrchestrator.ParticipantsStateService.ConnectedParticipants.Count);

            inCallController.RefreshCounter();
            communityVoiceChatSearchController.RefreshCounters();
        }

        private void OnParticipantStateRefreshed(List<(string participantId, VoiceChatParticipantsStateService.ParticipantState state)> joinedParticipants, List<string> leftParticipantIds)
        {
            if (!usedPlayerEntries.ContainsKey(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.WalletId))
            {
                if (voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker)
                    AddSpeaker(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState);
                else
                    AddListener(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState);
            }

            foreach ((string participantId, VoiceChatParticipantsStateService.ParticipantState state) participantData in joinedParticipants)
            {
                if (participantData.state.IsSpeaker)
                    AddSpeaker(participantData.state);
                else
                    AddListener(participantData.state);
            }

            foreach (string leftParticipantId in leftParticipantIds)
                RemoveParticipant(leftParticipantId);
        }

        private void RemoveParticipant(string leftParticipantId)
        {
            playerEntriesPool.Release(usedPlayerEntries[leftParticipantId]);
            usedPlayerEntries.Remove(leftParticipantId);
        }

        private void OnVoiceChatTypeChanged(VoiceChatType voiceChatType)
        {
            switch (voiceChatType)
            {
                case VoiceChatType.PRIVATE:
                    Hide();
                    break;
                case VoiceChatType.COMMUNITY:
                    Show();
                    view.SetConnectedPanel(false);
                    break;
                case VoiceChatType.NONE:
                default:
                    Hide();
                    break;
            }
        }

        private void Show()
        {
            view.gameObject.SetActive(true);
        }

        private void Hide()
        {
            view.gameObject.SetActive(false);
        }

        private void OnCollapsedButtonClicked()
        {
            isPanelCollapsed = !isPanelCollapsed;
            voiceChatOrchestrator.ChangePanelSize(isPanelCollapsed ? VoiceChatPanelSize.DEFAULT : VoiceChatPanelSize.EXPANDED);
            view.SetCollapsedButtonState(isPanelCollapsed);
        }

        private void AddSpeaker(VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry(participantState);
            inCallController.AddSpeaker(entryView);
        }

        private void AddListener(VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry(participantState);
            entryView.transform.parent = view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;
        }

        private PlayerEntryView GetAndConfigurePlayerEntry(VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            playerEntriesPool.Get(out PlayerEntryView entryView);
            usedPlayerEntries.Add(participantState.WalletId, entryView);

            entryView.ProfilePictureView.SetupAsync(profileRepositoryWrapper, ProfileNameColorHelper.GetNameColor(participantState.Name.Value), participantState.ProfilePictureUrl, participantState.WalletId, CancellationToken.None).Forget();
            entryView.nameElement.Setup(participantState.Name.Value, participantState.WalletId, participantState.HasClaimedName.Value ?? false, ProfileNameColorHelper.GetNameColor(participantState.Name.Value));

            view.ConfigureEntry(entryView, participantState, voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState);

            participantState.IsRequestingToSpeak.OnUpdate += isRequestingToSpeak => PlayerEntryIsRequestingToSpeak(isRequestingToSpeak, entryView);
            participantState.IsSpeaker.OnUpdate += isSpeaker => SetUserEntryParent(isSpeaker, entryView);
            participantState.IsRequestingToSpeak.OnUpdate += isRequestingToSpeak => SetUserRequestingToSpeak(isRequestingToSpeak, entryView, participantState.Name);

            return entryView;
        }

        private void SetUserRequestingToSpeak(bool isRequestingToSpeak, PlayerEntryView entryView, string playerName)
        {
            if (isRequestingToSpeak)
            {
                entryView.transform.parent = view.CommunityVoiceChatSearchView.RequestToSpeakParent;
                entryView.transform.localScale = Vector3.one;
                inCallController.ShowRaiseHandTooltip(playerName);
            }

            inCallController.RefreshCounter();
            communityVoiceChatSearchController.RefreshCounters();
        }

        private void SetUserEntryParent(bool isSpeaker, PlayerEntryView entryView)
        {
            entryView.transform.parent = isSpeaker ? inCallController.SpeakersParent : view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;

            communityVoiceChatSearchController.RefreshCounters();
            inCallController.RefreshCounter();
        }

        private void PlayerEntryIsRequestingToSpeak(bool? isRequestingToSpeak, PlayerEntryView entryView)
        {
            entryView.transform.parent = isRequestingToSpeak ?? false ? view.CommunityVoiceChatSearchView.RequestToSpeakParent : view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;
        }

        private void ClearPool()
        {
            foreach (KeyValuePair<string, PlayerEntryView> usedPlayerEntry in usedPlayerEntries)
                playerEntriesPool.Release(usedPlayerEntry.Value);

            usedPlayerEntries.Clear();
        }
    }
}
