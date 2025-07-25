using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Utilities;
using DG.Tweening;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityStreamButtonController : IDisposable
    {
        private const string OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT = "End your current call to start a new one.";
        private const float ANIMATION_DURATION = 0.5f;
        private const int WAIT_TIME_BEFORE_TOOLTIP_CLOSES_MS = 4000;

        private readonly IDisposable statusSubscription;
        private readonly IDisposable currentChannelSubscription;

        private readonly CallButtonView view;
        private readonly IVoiceChatOrchestrator orchestrator;
        private readonly IChatEventBus chatEventBus;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private readonly CommunitiesDataProvider communityDataProvider;
        private bool isClickedOnce;
        private bool isVoiceChatActive;
        private bool isCurrentCall;
        private CancellationTokenSource communityCts = new ();

        private CancellationTokenSource cts;

        public CommunityStreamButtonController(
            CallButtonView view,
            IVoiceChatOrchestrator orchestrator,
            IChatEventBus chatEventBus,
            IReadonlyReactiveProperty<ChatChannel> currentChannel,
            CommunitiesDataProvider communityDataProvider)
        {
            this.view = view;
            this.orchestrator = orchestrator;
            this.chatEventBus = chatEventBus;
            this.currentChannel = currentChannel;
            this.communityDataProvider = communityDataProvider;
            this.view.CallButton.onClick.AddListener(OnCallButtonClicked);
            cts = new CancellationTokenSource();

            statusSubscription = orchestrator.CurrentCallStatus.Subscribe(OnVoiceChatStatusChanged);
            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);

            // We might want to start the call directly here. And let the orchestrator handle the states.
            // But we will need to handle the parent view so it closes after the button is pressed and the call is successfully established (in case of Passport, etc.)
            chatEventBus.StartCall += OnCallButtonClicked;
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            chatEventBus.StartCall -= OnCallButtonClicked;
            view.CallButton.onClick.RemoveListener(OnCallButtonClicked);
            cts?.Dispose();
            communityCts?.Dispose();
        }

        public void Reset()
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

        private void OnCallButtonClicked()
        {
            cts = cts?.SafeRestart();
            HandleCallButtonClickAsync(cts!.Token).Forget();
        }

        private async UniTaskVoid HandleCallButtonClickAsync(CancellationToken ct)
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

            // Check if we're in a private call first
            if (orchestrator.CurrentVoiceChatType.Value == VoiceChatType.PRIVATE)
            {
                await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct);
                return;
            }

            // Check if we're already in a call
            if (orchestrator.CurrentCallStatus.Value is
                VoiceChatStatus.VOICE_CHAT_IN_CALL or
                VoiceChatStatus.VOICE_CHAT_STARTED_CALL or
                VoiceChatStatus.VOICE_CHAT_STARTING_CALL)
            {
                //Clicking the button finishes the call as we are already in this call
                if (isCurrentCall) {
                    //TODO: This behaviour was removed, now the button is not shown when the call is happening, keeping it for now just for ease of use.
                    orchestrator.HangUp();
                }

                //Clicking the button will show tooltip as we are in another call
                // TODO: We need to change this behaviour and make it end the current call and start a new one.
                else { await ShowTooltipWithAutoCloseAsync(OWN_USER_ALREADY_IN_CALL_TOOLTIP_TEXT, ct); }
            }
            else { orchestrator.StartCall(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), VoiceChatType.COMMUNITY); }
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

        private void OnVoiceChatStatusChanged(VoiceChatStatus newStatus)
        {
            if (newStatus == VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                // if the call that started is in this current channel, we need to hide START STREAM button
            }
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            //We hide it by default until we resolve if the user should see it.
            view.gameObject.SetActive(false);

            switch (newChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.COMMUNITY:
                    HandleChangeToCommunityChannelAsync(ChatChannel.GetCommunityIdFromChannelId(newChannel.Id)).Forget();
                    break;
                case ChatChannel.ChatChannelType.NEARBY:
                case ChatChannel.ChatChannelType.USER:
                case ChatChannel.ChatChannelType.UNDEFINED:
                    break;
            }

            Reset();
        }

        private async UniTaskVoid HandleChangeToCommunityChannelAsync(string communityId)
        {
            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);
            isVoiceChatActive = communityData.data.voiceChatStatus.isActive;
            bool isMod = communityData.data.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            isCurrentCall = false;

            if (!isMod) return;

            var shouldSeeButton = false;

            if (!isVoiceChatActive)
            {
                //Set Icon with Start Stream Image
                shouldSeeButton = true;
            }
            else if (orchestrator.CurrentCallId == communityId)
            {
                isCurrentCall = true;

                //Set Icon with Stop Stream Image
                shouldSeeButton = true;
            }

            view.gameObject.SetActive(shouldSeeButton);
        }
    }
}
