using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class CommunityStreamSubTitleButtonView : MonoBehaviour
    {
        [field: SerializeField] public Button JoinStreamButton { get; private set; }
        [field: SerializeField] public TMP_Text ParticipantsAmount { get; private set; }
    }
}
