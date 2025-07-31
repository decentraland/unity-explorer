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
        public Transform SpeakersParent => view.SpeakersParent;
        private CancellationTokenSource ct;

        public CommunityVoiceChatInCallController(CommunityVoiceChatInCallView view, IVoiceChatOrchestrator orchestrator)
        {
            this.view = view;
            footerController = new CommunityVoiceChatInCallFooterController(view.InCallFooterView, orchestrator);
            ct = new CancellationTokenSource();
        }

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
    }
}
