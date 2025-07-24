using DCL.UI;
using TMPro;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class CommunityStreamSubTitleBarView : MonoBehaviour
    {

        [field: SerializeField] public GameObject InStreamSign { get; private set; }
        [field: SerializeField] public CallButtonView JoinStreamButton { get; private set; }
        [field: SerializeField] public TMP_Text ParticipantsAmount { get; private set; }
    }
}
