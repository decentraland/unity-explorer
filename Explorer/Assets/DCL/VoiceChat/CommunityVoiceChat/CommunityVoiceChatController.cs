using DCL.Chat.EventBus;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Vector3 = UnityEngine.Vector3;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatController : IDisposable
    {
        private readonly CommunityVoiceChatTitlebarView view;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly IVoiceChatOrchestratorUIEvents voiceChatOrchestratorUIEvents;
        private readonly IVoiceChatOrchestratorState voiceChatOrchestratorState;
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
            IVoiceChatOrchestrator voiceChatOrchestrator)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            voiceChatOrchestratorUIEvents = voiceChatOrchestrator;
            voiceChatOrchestratorState = voiceChatOrchestrator;
            this.voiceChatOrchestrator = voiceChatOrchestrator;

            communityVoiceChatSearchController = new CommunityVoiceChatSearchController(view.CommunityVoiceChatSearchView);
            inCallController = new CommunityVoiceChatInCallController(view.CommunityVoiceChatInCallView, voiceChatOrchestrator);

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed += OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined += OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft += OnParticipantLeft;

            this.view.CollapseButtonClicked += OnCollapsedButtonClicked;
            this.view.PromoteToSpeaker += OnPromoteToSpeaker;
            this.view.DemoteSpeaker += OnDemoteSpeaker;
            this.view.Kick += OnKickUser;
            this.view.Ban += OnBanUser;

            // Should we send this through an internal event bus to avoid having these sub-view subscriptions or bubbling up events?
            view.CommunityVoiceChatInCallView.InCallFooterView.OpenListenersSectionButton.onClick.AddListener(OpenListenersSection);
            view.CommunityVoiceChatSearchView.BackButton.onClick.AddListener(CloseListenersSection);

            playerEntriesPool = new ObjectPool<PlayerEntryView>(
                () => Object.Instantiate(playerEntry),
                actionOnGet: entry => entry.gameObject.SetActive(true),
                actionOnRelease: entry => entry.gameObject.SetActive(false));

            voiceChatTypeSubscription = voiceChatOrchestratorState.CurrentVoiceChatType.Subscribe(OnVoiceChatTypeChanged);

            OnVoiceChatTypeChanged(voiceChatOrchestratorState.CurrentVoiceChatType.Value);

            //Temporary fix, this will be moved to the Show function to set expanded as default state
            voiceChatOrchestratorUIEvents.ChangePanelSize(VoiceChatPanelSize.EXPANDED);
        }

        public void Dispose()
        {
            view.CollapseButtonClicked -= OnCollapsedButtonClicked;
            view.PromoteToSpeaker -= OnPromoteToSpeaker;
            view.DemoteSpeaker -= OnDemoteSpeaker;
            view.Kick -= OnKickUser;
            view.Ban -= OnBanUser;
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
        }

        private void OpenListenersSection()
        {
            view.CommunityVoiceChatSearchView.gameObject.SetActive(true);
        }

        private void OnParticipantLeft(string participantid) =>
            RemoveParticipant(participantid);

        private void OnParticipantJoined(string participantid, VoiceChatParticipantsStateService.ParticipantState participantstate)
        {
            if (participantstate.IsSpeaker)
                AddSpeaker(participantstate);
            else
                AddListener(participantstate);
        }

        private void OnParticipantStateRefreshed(List<(string participantId, VoiceChatParticipantsStateService.ParticipantState state)> joinedParticipants, List<string> leftParticipantIds)
        {
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

        private void RemoveParticipant(string leftparticipantid)
        {
            playerEntriesPool.Release(usedPlayerEntries[leftparticipantid]);
            usedPlayerEntries.Remove(leftparticipantid);
        }

        private void OnPromoteToSpeaker(VoiceChatParticipantsStateService.ParticipantState member) { }

        private void OnDemoteSpeaker(VoiceChatParticipantsStateService.ParticipantState member) { }

        private void OnKickUser(VoiceChatParticipantsStateService.ParticipantState member) { }

        private void OnBanUser(VoiceChatParticipantsStateService.ParticipantState member) { }

        private void OnVoiceChatTypeChanged(VoiceChatType voiceChatType)
        {
            switch (voiceChatType)
            {
                case VoiceChatType.PRIVATE:
                    Hide();
                    break;
                case VoiceChatType.COMMUNITY:
                    Show();
                    break;
                case VoiceChatType.NONE:
                default:
                    Hide();
                    break;
            }
        }

        private void OnVoiceChatStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.VOICE_CHAT_IN_CALL:
                    view.Show();
                    break;
                case VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                    view.Hide();
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
            voiceChatOrchestratorUIEvents.ChangePanelSize(isPanelCollapsed ? VoiceChatPanelSize.DEFAULT : VoiceChatPanelSize.EXPANDED);
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

            //entryView.profileView.SetupAsync(new Web3Address(participantState.WalletId), profileRepositoryWrapper, CancellationToken.None).Forget();
            view.ConfigureEntry(entryView, participantState);

            participantState.IsRequestingToSpeak.OnUpdate += isRequestingToSpeak => PlayerEntryIsRequestingToSpeak(isRequestingToSpeak, entryView);

            return entryView;
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
