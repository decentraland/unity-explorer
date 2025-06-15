using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat
{
    public class CallButtonView : MonoBehaviour
    {
        [field: SerializeField]
        public Button CallButton { get; private set; }

        [field: SerializeField]
        public GameObject TooltipParent { get; private set; }

        [field: SerializeField]
        public TMP_Text TooltipText { get; private set; }
    }
}
