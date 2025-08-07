using DCL.Communities;
using DCL.UI;
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
        private readonly CommunityVoiceChatInCallFooterController footerController;

        public event Action EndStream;
        public Transform SpeakersParent => view.SpeakersParent;
        private CancellationTokenSource ct;
        private ImageController thumbnailController;

        public CommunityVoiceChatInCallController(
            CommunityVoiceChatInCallView view,
            IVoiceChatOrchestrator orchestrator,
            VoiceChatMicrophoneHandler microphoneHandler,
            IWebRequestController webRequestController)
        {
            this.view = view;
            footerController = new CommunityVoiceChatInCallFooterController(view.InCallFooterView, orchestrator, microphoneHandler);

            thumbnailController = new ImageController(view.CommunityThumbnail, webRequestController);
            view.EndStreamButton.onClick.AddListener(() => EndStream?.Invoke());
            ct = new CancellationTokenSource();
        }

        public void SetEndStreamButtonStatus(bool isActive) =>
            view.EndStreamButton.gameObject.SetActive(isActive);

        public void Dispose()
        {
            footerController.Dispose();
        }

        public void AddSpeaker(PlayerEntryView entryView)
        {
            entryView.transform.parent = view.SpeakersParent;
            entryView.transform.localScale = Vector3.one;
        }

        public void RefreshCounter()
        {
            view.SpeakersCount.text = string.Format("({0})", SpeakersParent.transform.childCount);
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
            thumbnailController.RequestImage(communityData.data.thumbnails.Value.raw);
        }
    }
}
