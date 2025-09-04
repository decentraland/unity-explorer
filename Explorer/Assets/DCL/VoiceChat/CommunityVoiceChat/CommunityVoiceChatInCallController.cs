using DCL.Audio;
using DCL.Communities;
using DCL.UI;
using DCL.Utilities;
using DCL.WebRequests;
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
        private readonly CommunityVoiceChatInCallButtonsController buttonsController;
        private readonly ImageController thumbnailController;
        private readonly IReadonlyReactiveProperty<VoiceChatPanelSize> currentVoiceChatPanelSize;

        public Transform SpeakersParent => view.SpeakersParent;
        private CancellationTokenSource ct;

        public CommunityVoiceChatInCallController(
            CommunityVoiceChatInCallView view,
            IVoiceChatOrchestrator voiceChatOrchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            IWebRequestController webRequestController)
        {
            this.view = view;
            this.voiceChatOrchestrator = voiceChatOrchestrator;
            buttonsController = new CommunityVoiceChatInCallButtonsController(view.InCallButtonsView, voiceChatOrchestrator, microphoneHandler);

            currentVoiceChatPanelSize = voiceChatOrchestrator.CurrentVoiceChatPanelSize;
            thumbnailController = new ImageController(view.CommunityThumbnail, webRequestController);
            view.EndStreamButton.onClick.AddListener(OnEndStreamButtonClicked);
            view.CommunityButton.onClick.AddListener(OnCommunityButtonClicked);
            view.CollapseButton.onClick.AddListener(OnCollapsedButtonClicked);
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
            buttonsController.Dispose();
            view.EndStreamButton.onClick.RemoveListener(OnEndStreamButtonClicked);
            view.CommunityButton.onClick.RemoveListener(OnCommunityButtonClicked);
            view.CollapseButton.onClick.RemoveListener(OnCollapsedButtonClicked);
        }

        public void AddSpeaker(PlayerEntryView entryView)
        {
            entryView.transform.parent = view.SpeakersParent;
            entryView.transform.localScale = Vector3.one;
        }

        public void RefreshCounter()
        {
            view.SpeakersCount.text = $"({SpeakersParent.transform.childCount})";
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
                thumbnailController.RequestImage(communityData.data.thumbnails.Value.raw);
        }

        public void SetTalkingStatus(int speakingCount, string username)
        {
            view.TalkingStatusView.SetSpeakingStatus(speakingCount, username);
        }

        private void OnCollapsedButtonClicked()
        {
            bool isPanelCollapsed = currentVoiceChatPanelSize.Value == VoiceChatPanelSize.DEFAULT;
            voiceChatOrchestrator.ChangePanelSize(isPanelCollapsed ? VoiceChatPanelSize.EXPANDED : VoiceChatPanelSize.DEFAULT);
            view.SetCollapsedState(isPanelCollapsed);
        }
    }
}
