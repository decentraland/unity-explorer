using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.WebRequests;
using MVC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatPresenter : IDisposable
    {
        private readonly CommunityVoiceChatPanelView view;
        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly VoiceChatRoomManager roomManager;
        private readonly IObjectPool<VoiceChatParticipantEntryView> playerEntriesPool;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly CommunityVoiceChatSearchPresenter communityVoiceChatSearchPresenter;
        private readonly CommunityVoiceChatInCallPresenter inCallPresenter;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly Dictionary<string, VoiceChatParticipantEntryPresenter> usedPlayerEntriesPresenters = new ();
        private readonly EventSubscriptionScope subscriptionsScope = new ();
        private readonly Dictionary<string, string> currentlySpeakingUsers = new ();

        private CancellationTokenSource cts = new ();
        private CancellationTokenSource popupCts = new ();
        private UniTaskCompletionSource contextMenuTask = new ();

        public CommunityVoiceChatPresenter(
            CommunityVoiceChatPanelView view,
            VoiceChatParticipantEntryView participantEntry,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            VoiceChatRoomManager roomManager,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.roomManager = roomManager;
            this.communityDataProvider = communityDataProvider;

            communityVoiceChatSearchPresenter = new CommunityVoiceChatSearchPresenter(view.CommunityVoiceChatSearchView);
            inCallPresenter = new CommunityVoiceChatInCallPresenter(view.CommunityVoiceChatInCallView, voiceChatOrchestrator, microphoneHandler, webRequestController, urlsSource);

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed += OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined += OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft += OnParticipantLeft;
            voiceChatOrchestrator.CommunityCallStatus.OnUpdate += OnCommunityCallStatusUpdate;
            voiceChatOrchestrator.CurrentCommunityId.OnUpdate += UpdateCommunityHeader;

            roomManager.ConnectionEstablished += OnConnectionEstablished;
            view.OpenListenersSection += OpenListenersSection;
            view.CloseListenersSection += CloseListenersSection;

            // Should we send this through an internal event bus to avoid having these sub-view subscriptions or bubbling up events?

            playerEntriesPool = new ObjectPool<VoiceChatParticipantEntryView>(
                () => Object.Instantiate(participantEntry),
                actionOnRelease: entry => entry.gameObject.SetActive(false));

            subscriptionsScope.Add(voiceChatOrchestrator.CurrentVoiceChatType.Subscribe(OnVoiceChatTypeChanged));
            subscriptionsScope.Add( voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.Subscribe(UpdateCounters));
            subscriptionsScope.Add(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsRequestingToSpeak.Subscribe(UpdateCounters));

            OnVoiceChatTypeChanged(voiceChatOrchestrator.CurrentVoiceChatType.Value);
        }

        private void UpdateCommunityHeader(string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return;

            cts = cts.SafeRestart();
            GetCommunityInfoAsync(communityId, cts.Token).Forget();
        }

        private void OnCommunityCallStatusUpdate(VoiceChatStatus status)
        {
            switch (status)
            {
                case VoiceChatStatus.DISCONNECTED:
                case VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    ClearPool();
                    break;
            }
        }

        public void Dispose()
        {
            roomManager.ConnectionEstablished -= OnConnectionEstablished;

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed -= OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined -= OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft -= OnParticipantLeft;
            voiceChatOrchestrator.CommunityCallStatus.OnUpdate -= OnCommunityCallStatusUpdate;

            subscriptionsScope.Dispose();
            communityVoiceChatSearchPresenter.Dispose();

            voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker.ClearSubscriptionsList();
            voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsRequestingToSpeak.ClearSubscriptionsList();

            contextMenuTask.TrySetResult();
            popupCts.SafeCancelAndDispose();

            ClearPool();
        }

        private void PromoteToSpeaker(string walletId)
        {
            voiceChatOrchestrator.PromoteToSpeakerInCurrentCall(walletId);
        }

        private void DenySpeaker(string walletId)
        {
            voiceChatOrchestrator.DenySpeakerInCurrentCall(walletId);
        }

        private void OnConnectionEstablished()
        {
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value == VoiceChatType.COMMUNITY)
            {
                view.SetConnectedPanel(true);
                bool isModeratorOrOwner = VoiceChatRoleHelper.IsModeratorOrOwner(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.Role.Value);
                communityVoiceChatSearchPresenter.SetGridCellSizes(isModeratorOrOwner);
                inCallPresenter.SetEndStreamButtonStatus(isModeratorOrOwner);
                UpdateCounters();
            }
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
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            RemoveParticipant(participantId);

            UpdateCounters();
        }

        private void OnParticipantJoined(string participantId, VoiceChatParticipantState participantState)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (participantState.IsSpeaker)
                AddSpeaker(participantState);
            else
                AddListener(participantState);

            UpdateCounters();
        }

        private void OnParticipantStateRefreshed(List<(string participantId, VoiceChatParticipantState state)> joinedParticipants, List<string> leftParticipantIds)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (!usedPlayerEntriesPresenters.ContainsKey(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.WalletId))
            {
                if (voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.IsSpeaker)
                    AddSpeaker(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState);
                else
                    AddListener(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState);
            }

            foreach ((string participantId, VoiceChatParticipantState state) participantData in joinedParticipants)
            {
                if (participantData.state.IsSpeaker)
                    AddSpeaker(participantData.state);
                else
                    AddListener(participantData.state);
            }

            foreach (string leftParticipantId in leftParticipantIds)
                RemoveParticipant(leftParticipantId);
        }

        private void RemoveParticipant(string removedParticipantId)
        {
            if (usedPlayerEntriesPresenters.Remove(removedParticipantId, out VoiceChatParticipantEntryPresenter entryPresenter))
            {
                entryPresenter.Dispose();
            }

            UpdateCounters();
        }

        private void OnVoiceChatTypeChanged(VoiceChatType voiceChatType)
        {
            switch (voiceChatType)
            {
                case VoiceChatType.COMMUNITY:
                    voiceChatOrchestrator.ChangePanelSize(VoiceChatPanelSize.EXPANDED);
                    view.Show();
                    break;
                case VoiceChatType.PRIVATE:
                case VoiceChatType.NONE:
                default:
                    contextMenuTask.TrySetResult();
                    popupCts.SafeCancelAndDispose();
                    view.Hide();
                    break;
            }
        }

        private async UniTaskVoid GetCommunityInfoAsync(string communityId, CancellationToken ct)
        {
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, ct);
            inCallPresenter.SetCommunityData(communityData);
        }

        private void AddSpeaker(VoiceChatParticipantState participantState)
        {
            VoiceChatParticipantEntryPresenter entryPresenter = GetAndConfigurePlayerEntry(participantState);
            entryPresenter.ConfigureAsSpeaker();
        }

        private void AddListener(VoiceChatParticipantState participantState)
        {
            VoiceChatParticipantEntryPresenter entryPresenter = GetAndConfigurePlayerEntry(participantState);
            entryPresenter.ConfigureAsListener();
        }

        private VoiceChatParticipantEntryPresenter GetAndConfigurePlayerEntry(VoiceChatParticipantState participantState)
        {
            playerEntriesPool.Get(out VoiceChatParticipantEntryView entryView);

            var newPresenter = new VoiceChatParticipantEntryPresenter(
                entryView,
                participantState,
                profileRepositoryWrapper,
                voiceChatOrchestrator,
                playerEntriesPool,
                view.CommunityVoiceChatSearchView.ListenersParent,
                view.CommunityVoiceChatInCallView.SpeakersParent,
                view.CommunityVoiceChatSearchView.RequestToSpeakParent);

            newPresenter.ApproveSpeaker += PromoteToSpeaker;
            newPresenter.DenySpeaker += DenySpeaker;
            newPresenter.UserIsRequestingToSpeak += OnUserIsRequestingToSpeak;
            newPresenter.ContextMenuClicked += OnContextMenuButtonClicked;

            subscriptionsScope.Add(participantState.IsRequestingToSpeak.Subscribe(UpdateCounters));
            subscriptionsScope.Add(participantState.IsSpeaker.Subscribe(UpdateCounters));
            subscriptionsScope.Add(participantState.IsSpeaking.Subscribe(isSpeaking => OnParticipantIsSpeaking(isSpeaking, participantState)));

            usedPlayerEntriesPresenters[participantState.WalletId] = newPresenter;
            return newPresenter;
        }

        private void OnParticipantIsSpeaking(bool isSpeaking, VoiceChatParticipantState participantState)
        {
            if (isSpeaking)
                currentlySpeakingUsers.Add(participantState.WalletId, participantState.Name.Value);
            else
                currentlySpeakingUsers.Remove(participantState.WalletId);

            inCallPresenter.SetTalkingStatus(currentlySpeakingUsers.Count, currentlySpeakingUsers.Count == 1 ? currentlySpeakingUsers.First().Value : string.Empty);
        }

        private void OnContextMenuButtonClicked(VoiceChatParticipantState participant, Vector2 buttonPosition)
        {
            popupCts = popupCts.SafeRestart();
            contextMenuTask.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();

            ViewDependencies.GlobalUIViews.ShowCommunityPlayerEntryContextMenuAsync(
                participant.WalletId,
                participant.IsSpeaker.Value,
                buttonPosition,
                default(Vector2),
                popupCts.Token,
                contextMenuTask.Task,
                anchorPoint: MenuAnchorPoint.BOTTOM_RIGHT).Forget();
        }

        private void OnUserIsRequestingToSpeak(string playerName)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.CommunityVoiceChatInCallView.RaiseHandAudio);
            inCallPresenter.ShowRaiseHandTooltip(playerName);
        }

        private void UpdateCounters(bool _)
        {
            UpdateCounters();
        }

        private void ClearPool()
        {
            foreach (var presenter in usedPlayerEntriesPresenters.Values)
            {
                presenter.Dispose();
            }

            usedPlayerEntriesPresenters.Clear();
        }

        private void UpdateCounters()
        {
            var speakers = 0;
            var listeners = 0;
            var raisedHands = 0;

            var localParticipant = voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState;

            if (localParticipant.IsSpeaker.Value)
                speakers++;
            else if (localParticipant.IsRequestingToSpeak.Value)
                raisedHands++;
            else
                listeners++;


            foreach (var participantEntry in usedPlayerEntriesPresenters)
            {
                string? participantId = participantEntry.Key;
                if (participantId == localParticipant?.WalletId) continue;

                if (voiceChatOrchestrator.ParticipantsStateService.TryGetParticipantState(participantId, out var participantState))
                {
                    if (participantState.IsSpeaker.Value)
                        speakers++;
                    else if (participantState.IsRequestingToSpeak.Value)
                        raisedHands++;
                    else
                        listeners++;
                }
            }

            inCallPresenter.RefreshCounters(speakers, raisedHands, voiceChatOrchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1);
            communityVoiceChatSearchPresenter.RefreshCounters(listeners, raisedHands);
        }
    }
}
