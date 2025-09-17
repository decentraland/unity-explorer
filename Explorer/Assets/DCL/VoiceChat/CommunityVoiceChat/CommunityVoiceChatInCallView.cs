using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.UI;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallView : MonoBehaviour
    {
        private const string TOOLTIP_CONTENT = "{0} requested to speak";

        [field: SerializeField]
        public TMP_Text CommunityName { get; private set; }

        [field: SerializeField]
        public Button CommunityButton { get; private set; }

        [field: SerializeField]
        public ImageView CommunityThumbnail { get; private set; }

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

        [field: FormerlySerializedAs("<InCallButtonsView>k__BackingField")]
        [field: FormerlySerializedAs("<InCallFooterView>k__BackingField")]
        [field: SerializeField]
        public CommunityVoiceChatInCallButtonsView ExpandedPanelInCallButtonsView { get; private set; }

        [field: SerializeField]
        public GameObject RaiseHandTooltip { get; private set; }

        [field: SerializeField]
        public TMP_Text RaiseHandTooltipText { get; private set; }

        [field: SerializeField]
        public Button EndStreamButton { get; private set; }

        [field: SerializeField]
        public Button OpenListenersSectionButton  { get; private set; }

        [field: SerializeField]
        public GameObject CollapseButtonImage  { get; private set; }

        [field: SerializeField]
        public GameObject Separator  { get; private set; }

        [field: SerializeField]
        public GameObject ExpandButtonImage  { get; private set; }

        [field: SerializeField]
        public Button CollapseButton  { get; private set; }

        [field: FormerlySerializedAs("<talkingStatusView>k__BackingField")]
        [field: SerializeField]
        public TalkingStatusView TalkingStatusView { get; private set; } = null!;

        [field: SerializeField] public CommunityVoiceChatInCallButtonsView CollapsedPanelInCallButtonsView { get; private set; } = null!;
        [field: SerializeField] public GameObject CollapsedPanelRightLayoutContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject ExpandedPanelRightLayoutContainer { get; private set; } = null!;

        [field: SerializeField] public AudioClipConfig EndStreamAudio { get; private set; } = null!;


        public void SetCommunityName(string communityName)
        {
            CommunityName.text = communityName;
        }

        public void SetParticipantCount(int participantCount)
        {
            ParticipantCount.text = $"{participantCount}";
        }

        public async UniTaskVoid ShowRaiseHandTooltipAndWaitAsync(string playerName, CancellationToken ct)
        {
            RaiseHandTooltipText.text = string.Format(TOOLTIP_CONTENT, playerName);
            RaiseHandTooltip.SetActive(true);
            await UniTask.Delay(5000, cancellationToken: ct);
            RaiseHandTooltip.SetActive(false);
        }

        public void SetCollapsedState(bool isCollapsed)
        {
            CollapsedPanelRightLayoutContainer.SetActive(isCollapsed);
            ExpandedPanelRightLayoutContainer.SetActive(!isCollapsed);
            CollapseButtonImage.SetActive(!isCollapsed);
            ExpandButtonImage.SetActive(isCollapsed);
            ContentPanel.SetActive(!isCollapsed);
            FooterPanel.SetActive(!isCollapsed);
            OpenListenersSectionButton.gameObject.SetActive(!isCollapsed);
            Separator.SetActive(!isCollapsed);
        }

        public void SetHiddenButtonsState(bool isHidden)
        {
            FooterPanel.SetActive(!isHidden);
        }
    }
}
