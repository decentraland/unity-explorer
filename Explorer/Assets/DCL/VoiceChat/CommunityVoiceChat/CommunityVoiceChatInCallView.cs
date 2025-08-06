using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallView : MonoBehaviour
    {
        private const string TOOLTIP_CONTENT = "{0} requested to speak";

        [field: SerializeField]
        public TMP_Text CommunityName { get; private set; }

        [field: SerializeField]
        public TMP_Text ParticipantCount { get; private set; }

        [field: SerializeField]
        public TMP_Text SpeakersCount { get; private set; }

        [field: SerializeField]
        public RectTransform SpeakersParent { get; private set; }

        [field: SerializeField]
        public GameObject ConnectingPanel { get; private set; }

        [field: SerializeField]
        public GameObject ContentPanel { get; private set; }

        [field: SerializeField]
        public GameObject FooterPanel { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatInCallFooterView InCallFooterView { get; private set; }

        [field: SerializeField]
        public GameObject RaiseHandTooltip { get; private set; }

        [field: SerializeField]
        public TMP_Text RaiseHandTooltipText { get; private set; }

        [field: SerializeField]
        public Button EndStreamButton { get; private set; }

        public void SetCommunityName(string communityName)
        {
            CommunityName.text = communityName;
        }

        public void SetParticipantCount(int participantCount)
        {
            ParticipantCount.text = string.Format("{0}", participantCount);
        }

        public async UniTaskVoid ShowRaiseHandTooltipAndWaitAsync(string playerName, CancellationToken ct)
        {
            RaiseHandTooltipText.text = string.Format(TOOLTIP_CONTENT, playerName);
            RaiseHandTooltip.SetActive(true);
            await UniTask.Delay(5000, cancellationToken: ct);
            RaiseHandTooltip.SetActive(false);
        }
    }
}
