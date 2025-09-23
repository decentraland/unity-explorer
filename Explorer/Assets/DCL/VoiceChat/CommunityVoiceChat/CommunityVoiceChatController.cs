using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;
using Utility;
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
        private readonly Dictionary<string, List<IDisposable>> participantSubscriptions = new ();
        private readonly CommunityVoiceChatSearchController communityVoiceChatSearchController;
        private readonly CommunityVoiceChatInCallController inCallController;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly IDisposable? voiceChatTypeSubscription;
        private readonly Dictionary<string, string> currentlySpeakingUsers = new();

        private CancellationTokenSource cts = new ();

        public CommunityVoiceChatController(
            CommunityVoiceChatTitlebarView view,
            PlayerEntryView playerEntry,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            VoiceChatRoomManager roomManager,
            CommunitiesDataProvider communityDataProvider,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            this.roomManager = roomManager;
            this.communityDataProvider = communityDataProvider;

            communityVoiceChatSearchController = new CommunityVoiceChatSearchController(view.CommunityVoiceChatSearchView);
            inCallController = new CommunityVoiceChatInCallController(view.CommunityVoiceChatInCallView, voiceChatOrchestrator, microphoneHandler, webRequestController);

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed += OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined += OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft += OnParticipantLeft;
            voiceChatOrchestrator.CommunityCallStatus.OnUpdate += OnCommunityCallStatusUpdate;
            voiceChatOrchestrator.CurrentCommunityId.OnUpdate += UpdateCommunityHeader;

            this.roomManager.ConnectionEstablished += OnConnectionEstablished;
            this.view.ApproveSpeaker += PromoteToSpeaker;
            this.view.DenySpeaker += DenySpeaker;

            // Should we send this through an internal event bus to avoid having these sub-view subscriptions or bubbling up events?
            view.CommunityVoiceChatInCallView.OpenListenersSectionButton.onClick.AddListener(OpenListenersSection);
            view.CommunityVoiceChatSearchView.BackButton.onClick.AddListener(CloseListenersSection);

            playerEntriesPool = new ObjectPool<PlayerEntryView>(
                () => Object.Instantiate(playerEntry),
                actionOnGet: OnGetPlayerEntry,
                actionOnRelease: entry => entry.gameObject.SetActive(false));

            voiceChatTypeSubscription = voiceChatOrchestrator.CurrentVoiceChatType.Subscribe(OnVoiceChatTypeChanged);

            OnVoiceChatTypeChanged(voiceChatOrchestrator.CurrentVoiceChatType.Value);

            //Temporary fix, this will be moved to the Show function to set expanded as default state
            voiceChatOrchestrator.ChangePanelSize(VoiceChatPanelSize.EXPANDED);
        }

        private void OnGetPlayerEntry(PlayerEntryView entry)
        {
            entry.gameObject.SetActive(true);
            entry.CleanupEntry();
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

            view.ApproveSpeaker -= PromoteToSpeaker;
            view.DenySpeaker -= DenySpeaker;

            voiceChatOrchestrator.ParticipantsStateService.ParticipantsStateRefreshed -= OnParticipantStateRefreshed;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantJoined -= OnParticipantJoined;
            voiceChatOrchestrator.ParticipantsStateService.ParticipantLeft -= OnParticipantLeft;
            voiceChatOrchestrator.CommunityCallStatus.OnUpdate -= OnCommunityCallStatusUpdate;

            voiceChatTypeSubscription?.Dispose();
            communityVoiceChatSearchController.Dispose();

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
                communityVoiceChatSearchController.SetGirdCellSizes(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.Role.Value is VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner);
                inCallController.SetEndStreamButtonStatus(voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState.Role.Value is VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner);
                RefreshCounters();
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
            inCallController.SetParticipantCount(voiceChatOrchestrator.ParticipantsStateService.ConnectedParticipants.Count);
        }

        private void OnParticipantJoined(string participantId, VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

            if (participantState.IsSpeaker)
                AddSpeaker(participantState);
            else
                AddListener(participantState);

            RefreshCounters();
        }

        private void OnParticipantStateRefreshed(List<(string participantId, VoiceChatParticipantsStateService.ParticipantState state)> joinedParticipants, List<string> leftParticipantIds)
        {
            if (voiceChatOrchestrator.CurrentVoiceChatType.Value != VoiceChatType.COMMUNITY) return;

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
            if (usedPlayerEntries.TryGetValue(leftParticipantId, out PlayerEntryView entry))
            {
                playerEntriesPool.Release(entry);
                if (participantSubscriptions.TryGetValue(leftParticipantId, out var subscriptions))
                {
                    foreach (var subscription in subscriptions)
                    {
                        subscription.Dispose();
                    }
                    participantSubscriptions.Remove(leftParticipantId);
                }
                usedPlayerEntries.Remove(leftParticipantId);
            }

            RefreshCounters();
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

        private async UniTaskVoid GetCommunityInfoAsync(string communityId, CancellationToken ct)
        {
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, ct);
            inCallController.SetCommunityData(communityData);
        }

        private void Show()
        {
            view.gameObject.SetActive(true);
        }

        private void Hide()
        {
            view.gameObject.SetActive(false);
        }

        private void AddSpeaker(VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry(participantState);
            entryView.isSpeakingIcon.gameObject.SetActive(true);
            inCallController.AddSpeaker(entryView);
        }

        private void AddListener(VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            PlayerEntryView entryView = GetAndConfigurePlayerEntry(participantState);
            entryView.isSpeakingIcon.gameObject.SetActive(false);
            entryView.transform.parent = view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;
        }

        private PlayerEntryView GetAndConfigurePlayerEntry(VoiceChatParticipantsStateService.ParticipantState participantState)
        {
            playerEntriesPool.Get(out PlayerEntryView entryView);
            usedPlayerEntries[participantState.WalletId] =  entryView;

            var nameColor = NameColorHelper.GetNameColor(participantState.Name.Value);

            entryView.ProfilePictureView.SetupAsync(profileRepositoryWrapper, nameColor, participantState.ProfilePictureUrl, participantState.WalletId, CancellationToken.None).Forget();
            entryView.nameElement.text = participantState.Name.Value;
            entryView.nameElement.color = NameColorHelper.GetNameColor(participantState.Name.Value);
            view.ConfigureEntry(entryView, participantState, voiceChatOrchestrator.ParticipantsStateService.LocalParticipantState);

            var subscriptions = new List<IDisposable>
            {
                participantState.IsSpeaking.Subscribe(isSpeaking => PlayerEntryIsSpeaking(isSpeaking, participantState.Name, participantState.WalletId)),
                participantState.IsRequestingToSpeak.Subscribe(isRequestingToSpeak => PlayerEntryIsRequestingToSpeak(isRequestingToSpeak, entryView)),
                participantState.IsSpeaker.Subscribe(isSpeaker => SetUserEntryParent(isSpeaker, entryView)),
                participantState.IsRequestingToSpeak.Subscribe(isRequestingToSpeak => SetUserRequestingToSpeak(isRequestingToSpeak, entryView, participantState.Name))
            };

            participantSubscriptions[participantState.WalletId] = subscriptions;
            return entryView;
        }

        private void PlayerEntryIsSpeaking(bool isSpeaking, ReactiveProperty<string?> participantStateName, string walletId)
        {
            if (isSpeaking)
                currentlySpeakingUsers.Add(walletId, participantStateName.Value);
            else
                currentlySpeakingUsers.Remove(walletId);

            inCallController.SetTalkingStatus(currentlySpeakingUsers.Count, currentlySpeakingUsers.Count == 1 ? currentlySpeakingUsers.First().Value : string.Empty);
        }

        private void SetUserRequestingToSpeak(bool isRequestingToSpeak, PlayerEntryView entryView, string playerName)
        {
            if (isRequestingToSpeak)
            {
                entryView.transform.parent = view.CommunityVoiceChatSearchView.RequestToSpeakParent;
                entryView.transform.localScale = Vector3.one;
                inCallController.ShowRaiseHandTooltip(playerName);
            }

            RefreshCounters();
        }

        private void SetUserEntryParent(bool isSpeaker, PlayerEntryView entryView)
        {
            entryView.isSpeakingIcon.gameObject.SetActive(isSpeaker);
            entryView.transform.parent = isSpeaker ? inCallController.SpeakersParent : view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;
            RefreshCounters();
        }

        private void PlayerEntryIsRequestingToSpeak(bool? isRequestingToSpeak, PlayerEntryView entryView)
        {
            entryView.transform.parent = isRequestingToSpeak ?? false ? view.CommunityVoiceChatSearchView.RequestToSpeakParent : view.CommunityVoiceChatSearchView.ListenersParent;
            entryView.transform.localScale = Vector3.one;
        }

        private void ClearPool()
        {
            foreach (var subscriptions in participantSubscriptions.Values)
            {
                foreach (var subscription in subscriptions)
                {
                    subscription.Dispose();
                }
            }
            participantSubscriptions.Clear();

            foreach (KeyValuePair<string, PlayerEntryView> usedPlayerEntry in usedPlayerEntries)
            {
                usedPlayerEntry.Value.CleanupEntry();
                playerEntriesPool.Release(usedPlayerEntry.Value);
            }

            usedPlayerEntries.Clear();
        }

        private void RefreshCounters()
        {
            inCallController.SetParticipantCount(voiceChatOrchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1);
            inCallController.RefreshCounter();
            communityVoiceChatSearchController.RefreshCounters();
        }
    }
}
