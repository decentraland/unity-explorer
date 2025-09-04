using Cysharp.Threading.Tasks;
using DCL.Audio;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class CommunityVoiceChatTitlebarView : MonoBehaviour
    {
        private const float SHOW_HIDE_ANIMATION_DURATION = 0.5f;

        [field: SerializeField]
        public CanvasGroup VoiceChatCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject VoiceChatContainer { get; private set; }


        [field: SerializeField]
        public CommunityVoiceChatInCallView CommunityVoiceChatInCallView { get; private set; }

        [field: SerializeField]
        public CommunityVoiceChatSearchView CommunityVoiceChatSearchView { get; private set; }

        [field: SerializeField]
        public AudioClipConfig EndStreamAudio { get; private set; }

        private CancellationTokenSource popupCts = new ();
        private UniTaskCompletionSource contextMenuTask = new ();

        public event Action<string> ApproveSpeaker;
        public event Action<string> DenySpeaker;

        private void OnContextMenuButtonClicked(VoiceChatParticipantsStateService.ParticipantState participant, Vector2 buttonPosition, PlayerEntryView elementView)
        {
            popupCts = popupCts.SafeRestart();
            contextMenuTask?.TrySetResult();
            contextMenuTask = new UniTaskCompletionSource();

            ViewDependencies.GlobalUIViews.ShowCommunityPlayerEntryContextMenuAsync(
                participant.WalletId,
                participant.IsSpeaker.Value,
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
