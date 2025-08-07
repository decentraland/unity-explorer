using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class SceneVoiceChatActiveCallView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text CommunityName { get; private set; }
        [field: SerializeField] public TMP_Text ParticipantCount { get; private set; }
        [field: SerializeField] public Button JoinStreamButton { get; private set; }

        public void SetCommunityName(string communityName)
        {
            CommunityName.text = communityName;
        }

        public void SetParticipantCount(int participantCount)
        {
            ParticipantCount.text = $"{participantCount}";
        }


    }
}
