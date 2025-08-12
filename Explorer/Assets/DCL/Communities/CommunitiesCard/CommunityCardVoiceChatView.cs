using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard
{
    public class CommunityCardVoiceChatView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject VoiceChatPanel;

        [field: SerializeField]
        public GameObject ModeratorControlPanel;

        [field: SerializeField]
        public GameObject LiveStreamPanel;

        [field: SerializeField]
        public Button StartStreamButton;

        [field: SerializeField]
        public Button EndStreamButton;

        [field: SerializeField]
        public Button JoinStreamButton;

        [field: SerializeField]
        public Button LeaveStreamButton;

        [field: SerializeField]
        public TMP_Text ListenersCount;
    }
}
