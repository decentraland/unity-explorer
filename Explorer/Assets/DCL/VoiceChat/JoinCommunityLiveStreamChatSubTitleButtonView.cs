using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class JoinCommunityLiveStreamChatSubTitleButtonView : MonoBehaviour
    {
        [field: SerializeField] public Button JoinStreamButton { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ParticipantsAmount { get; private set; } = null!;
    }
}
