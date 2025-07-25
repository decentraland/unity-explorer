using TMPro;
using UnityEngine;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text CommunityName { get; private set; }

        [field: SerializeField]
        public TMP_Text ParticipantCount { get; private set; }

        [field: SerializeField]
        public RectTransform SpeakersParent { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatInCallFooterView InCallFooterView { get; private set; }

        public void SetCommunityName(string communityName)
        {
            CommunityName.text = communityName;
        }

        public void SetParticipantCount(int participantCount)
        {
            ParticipantCount.text = string.Format("{0}", participantCount);
        }
    }
}
