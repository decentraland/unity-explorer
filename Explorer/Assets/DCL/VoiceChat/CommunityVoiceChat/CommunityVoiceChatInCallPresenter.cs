using DCL.Audio;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.UI;
using DCL.Utilities;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallPresenter : IDisposable
    {
        private const int MAX_VISIBLE_SPEAKERS = 8;

        private readonly CommunityVoiceChatInCallView view;

        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly CommunityVoiceChatInCallButtonsPresenter expandedPanelButtonsPresenter;
        private readonly CommunityVoiceChatInCallButtonsPresenter collapsedPanelButtonsPresenter;
        private readonly ImageController thumbnailController;
        private readonly IReadonlyReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize;
        private readonly IDisposable panelSizeChangeSubscription;
        private readonly IDisposable panelStateChangeSubscription;

        private int speakersCount;

        public event Action? OpenListenersSectionRequested;

        public Transform SpeakersParent => view.SpeakersParent;
        private CancellationTokenSource ct = new();

        public CommunityVoiceChatInCallPresenter(
            CommunityVoiceChatInCallView view,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            UITextureProvider textureProvider)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            expandedPanelButtonsPresenter = new CommunityVoiceChatInCallButtonsPresenter(view.ExpandedPanelInCallButtonsView, voiceChatOrchestrator, microphoneHandler);
            collapsedPanelButtonsPresenter = new CommunityVoiceChatInCallButtonsPresenter(view.CollapsedPanelInCallButtonsView, voiceChatOrchestrator, microphoneHandler);
            currentVoiceChatPanelSize = voiceChatOrchestrator.CurrentVoiceChatPanelSize;
            thumbnailController = new ImageController(view.CommunityThumbnail, textureProvider);

            view.EndStreamButtonCLicked += OnEndStreamButtonClicked;
            view.RaiseHandTooltipButtonCLicked += OnRaiseHandTooltipButtonClicked;
            view.CommunityButton.onClick.AddListener(OnCommunityButtonClicked);
            view.CollapseButton.onClick.AddListener(OnToggleCollapseButtonClicked);

            panelSizeChangeSubscription = currentVoiceChatPanelSize.Subscribe(OnPanelSizeChanged);
            panelStateChangeSubscription = voiceChatOrchestrator.CurrentVoiceChatPanelState.Subscribe(OnPanelStateChanged);
        }

        private void OnPanelStateChanged(VoiceChatPanelState state)
        {
            SetInCallElementsVisibility(voiceChatOrchestrator.CurrentVoiceChatPanelState.Value, currentVoiceChatPanelSize.Value);
        }

        private void OnPanelSizeChanged(VoiceChatPanelSize panelSize)
        {
            bool isPanelCollapsed = panelSize == VoiceChatPanelSize.COLLAPSED;
            view.SetCollapsedState(isPanelCollapsed);

            SetInCallElementsVisibility(voiceChatOrchestrator.CurrentVoiceChatPanelState.Value, panelSize);
        }

        private void SetInCallElementsVisibility(VoiceChatPanelState panelState, VoiceChatPanelSize panelSize)
        {
            view.SetButtonsVisibility(panelState is not VoiceChatPanelState.UNFOCUSED, panelSize);
            view.SetScrollAndMasksVisibility(speakersCount > MAX_VISIBLE_SPEAKERS);
        }

        private void OnCommunityButtonClicked()
        {
            string communityId = voiceChatOrchestrator.CurrentCommunityId.Value;
            if (!string.IsNullOrEmpty(communityId))
            {
                VoiceChatCommunityCardBridge.OpenCommunityCard(communityId);
            }
        }

        private void OnEndStreamButtonClicked()
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(view.EndStreamAudio);
            voiceChatOrchestrator.EndStreamInCurrentCall();
        }

        private void OnRaiseHandTooltipButtonClicked()
        {
            OpenListenersSectionRequested?.Invoke();
        }

        public void SetEndStreamButtonStatus(bool isActive) =>
            view.EndStreamButton.gameObject.SetActive(isActive);

        public void Dispose()
        {
            view.EndStreamButtonCLicked -= OnEndStreamButtonClicked;
            view.RaiseHandTooltipButtonCLicked -= OnRaiseHandTooltipButtonClicked;

            expandedPanelButtonsPresenter.Dispose();
            collapsedPanelButtonsPresenter.Dispose();
            panelSizeChangeSubscription.Dispose();
            panelStateChangeSubscription.Dispose();

            view.CommunityButton.onClick.RemoveListener(OnCommunityButtonClicked);
            view.CollapseButton.onClick.RemoveListener(OnToggleCollapseButtonClicked);
        }

        public void RefreshCounters(int updatedSpeakersCount, int raisedHandsCount, int totalParticipantCount)
        {
            speakersCount = updatedSpeakersCount;
            view.SpeakersCount.text = $"({updatedSpeakersCount})";
            view.ConfigureRaisedHandTooltip(raisedHandsCount);
            view.SetParticipantCount(totalParticipantCount);
            view.SetScrollAndMasksVisibility(updatedSpeakersCount > MAX_VISIBLE_SPEAKERS);
        }

        public void ShowRaiseHandTooltip(string? playerName)
        {
            ct = ct.SafeRestart();
            view.ShowRaiseHandTooltipAndWaitAsync(playerName, ct.Token).Forget();
        }

        public void SetCommunityData(GetCommunityResponse communityData)
        {
            view.SetCommunityName(communityData.data.name);
            thumbnailController.RequestImage(communityData.data.thumbnailUrl, useKtx: true, defaultSprite: view.DefaultCommunitySprite);
        }

        public void SetTalkingStatus(int speakingCount, string username)
        {
            view.TalkingStatusView.SetSpeakingStatus(speakingCount, username);
        }

        private void OnToggleCollapseButtonClicked()
        {
            bool isPanelCollapsed = currentVoiceChatPanelSize.Value == VoiceChatPanelSize.COLLAPSED;
            voiceChatOrchestrator.ChangePanelSize(isPanelCollapsed ? VoiceChatPanelSize.EXPANDED : VoiceChatPanelSize.COLLAPSED);
        }
    }
}
