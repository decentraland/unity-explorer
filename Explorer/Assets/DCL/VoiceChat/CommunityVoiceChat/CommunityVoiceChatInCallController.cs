using System;
using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallController : IDisposable
    {
        private readonly CommunityVoiceChatInCallView view;
        private readonly CommunityVoiceChatInCallFooterController footerController;
        public Transform SpeakersParent => view.SpeakersParent;

        private int speakersCount = 0;

        public CommunityVoiceChatInCallController(CommunityVoiceChatInCallView view, IVoiceChatOrchestrator orchestrator)
        {
            this.view = view;
            footerController = new CommunityVoiceChatInCallFooterController(view.InCallFooterView, orchestrator);
        }

        public void Dispose()
        {
            footerController.Dispose();
        }

        public void AddSpeaker(PlayerEntryView entryView)
        {
            speakersCount++;
            view.ParticipantCount.text = speakersCount.ToString();
            entryView.transform.parent = view.SpeakersParent;
            entryView.transform.localScale = Vector3.one;
        }

        public void RemoveSpeaker()
        {
            speakersCount--;
        }
    }
}
