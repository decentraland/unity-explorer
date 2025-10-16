using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatInCallView : MonoBehaviour
    {
        private const string TOOLTIP_CONTENT = "{0} requested to speak";
        private const string END_COMMUNITY_STREAM_TEXT_FORMAT = "Are you sure you want to end {0}'s live voice stream?";
        private const string END_COMMUNITY_STREAM_CONFIRM_TEXT = "YES";
        private const string END_COMMUNITY_STREAM_CANCEL_TEXT = "NO";
        private const string DEFAULT_NAME = "[Missing Name]";

        private static readonly Vector2 RAISE_HAND_TOOLTIP_COLLAPSED_POSITION = new Vector2(199, -23);
        private static readonly Vector2 RAISE_HAND_TOOLTIP_NORMAL_POSITION = new Vector2(199, -66);

        public event Action? EndStreamButtonCLicked;

        [field: SerializeField]
        public TMP_Text CommunityName { get; private set; } = null!;

        [field: SerializeField]
        public Button CommunityButton { get; private set; } = null!;

        [field: SerializeField]
        public Sprite DefaultCommunitySprite { get; private set; } = null!;

        [field: SerializeField]
        public ImageView CommunityThumbnail { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text ParticipantCount { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text SpeakersCount { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform SpeakersParent { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ConnectingPanel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ContentPanel { get; private set; } = null!;

        [field: SerializeField]
        public GameObject FooterPanel { get; private set; } = null!;

        [field: SerializeField]
        public CommunityVoiceChatInCallButtonsView ExpandedPanelInCallButtonsView { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform RaiseHandTooltip { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text RaiseHandTooltipText { get; private set; } = null!;

        [field: SerializeField]
        public Button EndStreamButton { get; private set; } = null!;

        [field: SerializeField]
        public Button OpenListenersSectionButton  { get; private set; } = null!;

        [field: SerializeField]
        public GameObject CollapseButtonImage  { get; private set; } = null!;

        [field: SerializeField]
        public GameObject Separator  { get; private set; } = null!;

        [field: SerializeField]
        public GameObject ExpandButtonImage  { get; private set; } = null!;

        [field: SerializeField]
        public Button CollapseButton  { get; private set; } = null!;
        [field: SerializeField]
        public GameObject RaisedHandTooltip  { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text RaisedHandTooltipText  { get; private set; } = null!;

        [field: FormerlySerializedAs("<talkingStatusView>k__BackingField")]
        [field: SerializeField]
        public TalkingStatusView TalkingStatusView { get; private set; } = null!;

        [field: SerializeField] public CommunityVoiceChatInCallButtonsView CollapsedPanelInCallButtonsView { get; private set; } = null!;
        [field: SerializeField] public GameObject CollapsedPanelRightLayoutContainer { get; private set; } = null!;
        [field: SerializeField] public GameObject ExpandedPanelRightLayoutContainer { get; private set; } = null!;
        [field: SerializeField] public Image MaskImage { get; private set; } = null!;
        [field: SerializeField] public ScrollRect ScrollRect { get; private set; } = null!;
        [field: SerializeField] public RectMask2D RectMask2D { get; private set; } = null!;

        [field: SerializeField] public AudioClipConfig EndStreamAudio { get; private set; } = null!;
        [field: SerializeField] public AudioClipConfig RaiseHandAudio { get; private set; } = null!;

        private CancellationTokenSource? endStreamButtonConfirmationDialogCts;

        public void Start()
        {
            EndStreamButton.onClick.AddListener(() =>
            {
                endStreamButtonConfirmationDialogCts = endStreamButtonConfirmationDialogCts.SafeRestart();
                ShowDeleteInvitationConfirmationDialogAsync(endStreamButtonConfirmationDialogCts.Token).Forget();
                return;

                async UniTask ShowDeleteInvitationConfirmationDialogAsync(CancellationToken ct)
                {
                    Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(END_COMMUNITY_STREAM_TEXT_FORMAT, CommunityName.text),
                                                                                         END_COMMUNITY_STREAM_CANCEL_TEXT,
                                                                                         END_COMMUNITY_STREAM_CONFIRM_TEXT,
                                                                                         default,
                                                                                         false, false), ct)
                                                                                    .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                    if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                    EndStreamButtonCLicked?.Invoke();
                }
            });
        }

        public void ConfigureRaisedHandTooltip(int raisedHandCount)
        {
            RaisedHandTooltipText.text = $"{raisedHandCount}";
            RaisedHandTooltip.SetActive(raisedHandCount >= 1);
        }

        public void SetCommunityName(string communityName)
        {
            CommunityName.text = communityName;
        }

        public void SetParticipantCount(int participantCount)
        {
            ParticipantCount.text = $"{participantCount}";
        }

        public async UniTaskVoid ShowRaiseHandTooltipAndWaitAsync(string? playerName, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(playerName)) playerName = DEFAULT_NAME;

            RaiseHandTooltipText.text = string.Format(TOOLTIP_CONTENT, playerName);
            RaiseHandTooltip.gameObject.SetActive(true);
            await UniTask.Delay(5000, cancellationToken: ct);
            RaiseHandTooltip.gameObject.SetActive(false);
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
            RaiseHandTooltip.anchoredPosition = isCollapsed ? RAISE_HAND_TOOLTIP_COLLAPSED_POSITION : RAISE_HAND_TOOLTIP_NORMAL_POSITION;
        }

        public void SetButtonsVisibility(bool isVisible, VoiceChatPanelSize size)
        {
            bool showExpanded = isVisible && size is VoiceChatPanelSize.EXPANDED;
            bool showCollapsed = isVisible && size is VoiceChatPanelSize.COLLAPSED;

            FooterPanel.SetActive(showExpanded);
            ExpandedPanelRightLayoutContainer.SetActive(showExpanded);
            CollapsedPanelRightLayoutContainer.SetActive(showCollapsed);
        }

        public void SetScrollAndMasksVisibility(bool isVisible)
        {
            ScrollRect.vertical = isVisible;
            MaskImage.enabled = isVisible;
            RectMask2D.enabled = isVisible;
        }
    }
}
