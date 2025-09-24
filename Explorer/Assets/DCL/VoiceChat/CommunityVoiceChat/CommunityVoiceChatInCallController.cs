using DCL.Audio;
using DCL.Communities;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.Utilities;
using DCL.WebRequests;
using NSubstitute.Extensions;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallController : IDisposable
    {
        private readonly CommunityVoiceChatInCallView view;

        private readonly IVoiceChatOrchestrator voiceChatOrchestrator;
        private readonly CommunityVoiceChatInCallButtonsPresenter expandedPanelButtonsPresenter;
        private readonly CommunityVoiceChatInCallButtonsPresenter collapsedPanelButtonsPresenter;
        private readonly ImageController thumbnailController;
        private readonly IReadonlyReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize;
        private readonly IDisposable panelSizeChangeSubscription;

        public Transform SpeakersParent => view.SpeakersParent;
        private CancellationTokenSource ct = new();

        public CommunityVoiceChatInCallController(
            CommunityVoiceChatInCallView view,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            expandedPanelButtonsPresenter = new CommunityVoiceChatInCallButtonsPresenter(view.ExpandedPanelInCallButtonsView, voiceChatOrchestrator, microphoneHandler);
            collapsedPanelButtonsPresenter = new CommunityVoiceChatInCallButtonsPresenter(view.CollapsedPanelInCallButtonsView, voiceChatOrchestrator, microphoneHandler);
            currentVoiceChatPanelSize = voiceChatOrchestrator.CurrentVoiceChatPanelSize;
            thumbnailController = new ImageController(view.CommunityThumbnail, webRequestController);

            view.EndStreamButtonCLicked += OnEndStreamButtonClicked;
            view.CommunityButton.onClick.AddListener(OnCommunityButtonClicked);
            view.CollapseButton.onClick.AddListener(OnToggleCollapseButtonClicked);

            panelSizeChangeSubscription = currentVoiceChatPanelSize.Subscribe(OnPanelSizeChanged);
        }

        private void OnPanelSizeChanged(VoiceChatPanelSize panelSize)
        {
            view.SetHiddenButtonsState(panelSize is VoiceChatPanelSize.EXPANDED_WITHOUT_BUTTONS or VoiceChatPanelSize.COLLAPSED);
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

        public void SetEndStreamButtonStatus(bool isActive) =>
            view.EndStreamButton.gameObject.SetActive(isActive);

        public void Dispose()
        {
            view.EndStreamButtonCLicked -= OnEndStreamButtonClicked;

            expandedPanelButtonsPresenter.Dispose();
            collapsedPanelButtonsPresenter.Dispose();
            panelSizeChangeSubscription.Dispose();

            view.CommunityButton.onClick.RemoveListener(OnCommunityButtonClicked);
            view.CollapseButton.onClick.RemoveListener(OnToggleCollapseButtonClicked);
        }

        public void AddSpeaker(PlayerEntryView entryView)
        {
            entryView.transform.parent = view.SpeakersParent;
            entryView.transform.localScale = Vector3.one;
        }

        public void RefreshCounter(int count, int raisedHandsCount)
        {
            view.SpeakersCount.text = $"({count})";
            view.ConfigureRaisedHandTooltip(raisedHandsCount);
        }

        public void SetParticipantCount(int participantCount)
        {
            view.SetParticipantCount(participantCount);
        }

        public void ShowRaiseHandTooltip(string playerName)
        {
            ct = ct.SafeRestart();
            view.ShowRaiseHandTooltipAndWaitAsync(playerName, ct.Token).Forget();
        }

        public void SetCommunityData(GetCommunityResponse communityData)
        {
            view.SetCommunityName(communityData.data.name);
            if (communityData.data.thumbnails != null)
                thumbnailController.RequestImage(communityData.data.thumbnails.Value.raw, defaultSprite: view.DefaultCommunitySprite);
            else
                view.CommunityThumbnail.SetImage(view.DefaultCommunitySprite);
        }

        public void SetTalkingStatus(int speakingCount, string username)
        {
            view.TalkingStatusView.SetSpeakingStatus(speakingCount, username);
        }

        private void OnToggleCollapseButtonClicked()
        {
            bool isPanelCollapsed = currentVoiceChatPanelSize.Value == VoiceChatPanelSize.COLLAPSED;
            voiceChatOrchestrator.ChangePanelSize(isPanelCollapsed ? VoiceChatPanelSize.EXPANDED : VoiceChatPanelSize.COLLAPSED);
            view.SetCollapsedState(!isPanelCollapsed);
        }
    }
}
