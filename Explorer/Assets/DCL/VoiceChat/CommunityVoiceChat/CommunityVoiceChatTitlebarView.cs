using Cysharp.Threading.Tasks;
using DG.Tweening;
using DCL.Communities;
using DCL.UI;
using DCL.VoiceChat;
using DCL.Web3;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatTitlebarView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        public event Action CollapseButtonClicked;

        [field: SerializeField]
        public CanvasGroup VoiceChatCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject VoiceChatContainer { get; private set; }

        [field: SerializeField]
        public Button CollapseButton  { get; private set; }

        [field: SerializeField]
        public Sprite CollapseButtonImage { get; private set; }

        [field: SerializeField]
        public Sprite UnCollapseButtonImage { get; private set; }

        [field: SerializeField]
        public RectTransform HeaderContainer { get; private set; }

        [field: SerializeField]
        public RectTransform ContentContainer { get; private set; }

        [field: SerializeField]
        public RectTransform FooterContainer { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatInCallView CommunityVoiceChatInCallView { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatSearchView CommunityVoiceChatSearchView { get; private set; }

        private CancellationTokenSource popupCts = new ();
        private UniTaskCompletionSource contextMenuTask = new ();

        public event Action<string> ApproveSpeaker;
        public event Action<string> DenySpeaker;

        private void Start()
        {
            CollapseButton.onClick.AddListener(() => CollapseButtonClicked?.Invoke());
        }

        private void OnContextMenuButtonClicked(VoiceChatParticipantsStateService.ParticipantState voiceChatMember, VoiceChatParticipantsStateService.ParticipantState localParticipantState, Vector2 buttonPosition, PlayerEntryView elementView)
        {
            popupCts = popupCts.SafeRestart();
            contextMenuTask?.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();

            bool isModeratorOrAdmin = localParticipantState.Role.Value is VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner;

            ViewDependencies.GlobalUIViews.ShowCommunityPlayerEntryContextMenuAsync(
                participantWalletId: voiceChatMember.WalletId,
                isSpeaker: voiceChatMember.IsSpeaker.Value,
                isModeratorOrAdmin: isModeratorOrAdmin,
                buttonPosition,
                default(Vector2),
                popupCts.Token,
                contextMenuTask.Task,
                anchorPoint: MenuAnchorPoint.BOTTOM_RIGHT).Forget();
        }

        public void ConfigureEntry(PlayerEntryView entryView, VoiceChatParticipantsStateService.ParticipantState participantState, VoiceChatParticipantsStateService.ParticipantState localParticipantState)
        {
            entryView.SubscribeToInteractions(OnContextMenuButtonClicked, ApproveSpeaker, DenySpeaker);
            entryView.SetUserProfile(participantState, localParticipantState);
        }

        public void SetCollapsedButtonState(bool isCollapsed)
        {
            ContentContainer.gameObject.SetActive(!isCollapsed);
            FooterContainer.gameObject.SetActive(!isCollapsed);
            CollapseButton.image.sprite = isCollapsed ? UnCollapseButtonImage : CollapseButtonImage;
        }

        public void Show()
        {
            VoiceChatContainer.SetActive(true);
            VoiceChatCanvasGroup.alpha = 0;
            VoiceChatCanvasGroup
               .DOFade(1, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(true);
                    VoiceChatCanvasGroup.alpha = 1;
                });
        }

        public void Hide()
        {
            contextMenuTask?.TrySetResult();
            popupCts.SafeCancelAndDispose();

            VoiceChatCanvasGroup.alpha = 1;
            VoiceChatCanvasGroup
               .DOFade(0, SHOW_HIDE_ANIMATION_DURATION)
               .SetEase(Ease.Flash)
               .OnComplete(() =>
                {
                    VoiceChatContainer.SetActive(false);
                    VoiceChatCanvasGroup.alpha = 0;
                });
        }

        public void SetConnectedPanel(bool isConnected)
        {
            CommunityVoiceChatInCallView.ConnectingPanel.SetActive(!isConnected);
            CommunityVoiceChatInCallView.ContentPanel.SetActive(isConnected);
            CommunityVoiceChatInCallView.FooterPanel.SetActive(isConnected);
        }

        private void OnDestroy()
        {
            contextMenuTask?.TrySetResult();
            popupCts.SafeCancelAndDispose();
        }
    }
}
