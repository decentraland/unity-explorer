using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityStreamButtonController : IDisposable
    {
        private readonly IDisposable currentChannelSubscription;

        private readonly CallButtonView view;
        private readonly IVoiceChatOrchestrator orchestrator;
        private readonly IChatEventBus chatEventBus;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private readonly CommunitiesDataProvider communityDataProvider;

        private CancellationTokenSource communityCts = new ();
        private IDisposable currentCommunityCallStatusSubscription;

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
            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);

            // We might want to start the call directly here. And let the orchestrator handle the states.
            // We will need to handle the parent view so it closes after the button is pressed and the call is successfully established (in case of Passport, etc.)
            chatEventBus.StartCall += OnCallButtonClicked;
        }

        public void Dispose()
        {
            currentChannelSubscription?.Dispose();
            chatEventBus.StartCall -= OnCallButtonClicked;
            view.CallButton.onClick.RemoveListener(OnCallButtonClicked);
            communityCts?.Dispose();
        }

        public void Reset()
        {
            if (!PlayerLoopHelper.IsMainThread)
                ResetAsync().Forget();
            else
                view.TooltipParent.gameObject.SetActive(false);
        }

        private async UniTaskVoid ResetAsync()
        {
            await UniTask.SwitchToMainThread();
        }

        private void OnCallButtonClicked()
        {
            orchestrator.HangUp();
            orchestrator.StartCall(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), VoiceChatType.COMMUNITY);
        }

        private void OnCurrentCommunityCallStatusChanged(bool hasActiveCall)
        {
            //We show the button if the current community doesn't have an active call
            view.gameObject.SetActive(!hasActiveCall);
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            //We hide it by default until we resolve if the user should see it.
            view.gameObject.SetActive(false);
            currentCommunityCallStatusSubscription.Dispose();

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
            bool isVoiceChatActive = communityData.data.voiceChatStatus.isActive;
            bool isMod = communityData.data.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;

            if (!isMod) return;

            bool shouldSeeButton = !isVoiceChatActive;

            currentCommunityCallStatusSubscription = orchestrator.CommunityStatusService.SubscribeToCommunityUpdates(communityId)?.Subscribe(OnCurrentCommunityCallStatusChanged);

            view.gameObject.SetActive(shouldSeeButton);
        }
    }
}
