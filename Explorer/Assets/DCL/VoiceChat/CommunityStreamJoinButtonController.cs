using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Utilities;
using DG.Tweening;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityStreamJoinButtonController : IDisposable
    {
        private const string OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "End your current call to start a new one.";
        private const float ANIMATION_DURATION = 0.5f;
        private const int WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS = 4000;

        private readonly IDisposable currentChannelSubscription;

        private readonly CallButtonView view;
        private readonly ICommunityCallOrchestrator communityCallOrchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private bool isClickedOnce;

        private CancellationTokenSource cts;

        public CommunityStreamJoinButtonController(
            CallButtonView view,
            ICommunityCallOrchestrator communityCallOrchestrator,
            IReadonlyReactiveProperty<ChatChannel> currentChannel)
        {
            this.view = view;
            this.communityCallOrchestrator = communityCallOrchestrator;
            this.currentChannel = currentChannel;
            this.view.CallButton.onClick.AddListener(OnJoinButtonClicked);
            cts = new CancellationTokenSource();

            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
        }

        public void Dispose()
        {
            currentChannelSubscription?.Dispose();
            view.CallButton.onClick.RemoveListener(OnJoinButtonClicked);
            cts?.Dispose();
        }

        private void Reset()
        {
            if (!PlayerLoopHelper.IsMainThread)
                ResetAsync().Forget();
            else
                view.TooltipParent.gameObject.SetActive(false);

            isClickedOnce = false;
        }

        private async UniTaskVoid ResetAsync()
        {
            await UniTask.SwitchToMainThread();
            isClickedOnce = false;
            view.TooltipParent.gameObject.SetActive(false);
        }

        private void OnJoinButtonClicked()
        {
            cts = cts?.SafeRestart();
            HandleJoinButtonClickAsync(cts!.Token).Forget();
        }

        private async UniTaskVoid HandleJoinButtonClickAsync(CancellationToken ct)
        {
            if (isClickedOnce)
            {
                // If already clicked once, immediately hide tooltip and reset state
                view.TooltipParent.gameObject.SetActive(false);
                isClickedOnce = false;
                return;
            }

            // First click - set the flag and handle the logic
            isClickedOnce = true;

            // Check if we're already in any call
            if (communityCallOrchestrator.CurrentCallStatus.Value is
                VoiceChatStatus.VOICE_CHAT_IN_CALL or
                VoiceChatStatus.VOICE_CHAT_STARTED_CALL or
                VoiceChatStatus.VOICE_CHAT_STARTING_CALL)
            {
                // Show tooltip that we're already in a call
                await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
            }
            else
            {
                // Join the call for the current community channel
                string communityId = ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id);
                communityCallOrchestrator.JoinCommunityVoiceChat(communityId, ct);
            }
        }

        private async UniTask ShowTooltipWithAutoCloseAsync(string tooltipText, CancellationToken ct)
        {
            view.TooltipParentCanvas.alpha = 0;
            view.TooltipParent.gameObject.SetActive(true);
            view.TooltipParentCanvas.interactable = true;
            view.TooltipParentCanvas.blocksRaycasts = true;
            view.TooltipText.text = tooltipText;

            await view.TooltipParentCanvas.DOFade(1, ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            await UniTask.Delay(WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS, cancellationToken: ct);
            await view.TooltipParentCanvas.DOFade(0, ANIMATION_DURATION).ToUniTask(cancellationToken: ct);
            view.TooltipParent.gameObject.SetActive(false);
            view.TooltipParentCanvas.interactable = false;
            view.TooltipParentCanvas.blocksRaycasts = false;
            isClickedOnce = false;
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            // Reset state when channel changes
            Reset();
        }
    }
}
