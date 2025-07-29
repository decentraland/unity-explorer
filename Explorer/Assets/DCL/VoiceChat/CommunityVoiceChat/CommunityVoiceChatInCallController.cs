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
            IncreaseSpeakerCounter();
            entryView.transform.parent = view.SpeakersParent;
            entryView.transform.localScale = Vector3.one;
        }

        public void IncreaseSpeakerCounter()
        {
            speakersCount++;
            view.SpeakersCount.text = string.Format("({0})", speakersCount);
        }

        public void DecreaseSpeakerCounter()
        {
            speakersCount--;
            view.SpeakersCount.text = string.Format("({0})", speakersCount);
        }

        public void SetParticipantCount(int participantCount)
        {
            view.SetParticipantCount(participantCount);
        }
    }
}
