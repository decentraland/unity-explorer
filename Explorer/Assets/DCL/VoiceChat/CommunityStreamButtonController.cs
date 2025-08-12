using Cysharp.Threading.Tasks;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityStreamButtonController : IDisposable
    {
        private const string TAG = nameof(CommunityStreamButtonController);

        private readonly IDisposable currentChannelSubscription;
        private readonly IDisposable communityCallStateSubscription;

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
            communityCallStateSubscription = orchestrator.CommunityCallStatus.Subscribe(OnCommunityCallStatusChanged);
            chatEventBus.StartCall += OnCallButtonClicked;
        }

        public void Dispose()
        {
            currentChannelSubscription.Dispose();
            communityCallStateSubscription.Dispose();
            chatEventBus.StartCall -= OnCallButtonClicked;
            view.CallButton.onClick.RemoveListener(OnCallButtonClicked);
            communityCts.SafeCancelAndDispose();
        }

        private void OnCommunityCallStatusChanged(VoiceChatStatus status)
        {
            if (status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR or VoiceChatStatus.VOICE_CHAT_ENDING_CALL or VoiceChatStatus.VOICE_CHAT_BUSY)
                OnCurrentChannelChanged(currentChannel.Value);
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

        private void OnCurrentCommunityActiveCallStatusChanged(bool hasActiveCall)
        {
            // We show the button if the current community doesn't have an active call
            // We only get this update if we were mods of that community when we entered the channel.
            OnCurrentCommunityActiveCallStatusChangedAsync().Forget();
            return;

            async UniTaskVoid OnCurrentCommunityActiveCallStatusChangedAsync()
            {
                string communityId = ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id);
                GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);

                //We check again if we are mods as this data might have changed in between we entered the channel and we left it
                bool isMod = communityData.data.role.IsAnyMod();

                if (!isMod)
                {
                    view.gameObject.SetActive(false);
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: User is not moderator/owner for community {communityId}, keeping button hidden");
                    return;
                }

                bool shouldBeActive = !hasActiveCall;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCurrentCommunityActiveCallStatusChanged: Setting button active={shouldBeActive} (hasActiveCall={hasActiveCall})");
                view.gameObject.SetActive(shouldBeActive);
            }
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            //We hide it by default until we resolve if the user should see it.
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCurrentChannelChanged: Hiding button by default for channel type {newChannel.ChannelType}");
            view.gameObject.SetActive(false);
            currentCommunityCallStatusSubscription?.Dispose();

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

            bool isMod = communityData.data.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
            if (!isMod)
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: User is not moderator/owner for community {communityId}, keeping button hidden");
                return;
            }

            bool isVoiceChatActive = communityData.data.voiceChatStatus.isActive;
            bool shouldSeeButton = !isVoiceChatActive;
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: Setting button active={shouldSeeButton} for community {communityId} (isVoiceChatActive={isVoiceChatActive}");
            view.gameObject.SetActive(shouldSeeButton);

            currentCommunityCallStatusSubscription = orchestrator.SubscribeToCommunityUpdates(communityId)?.Subscribe(OnCurrentCommunityActiveCallStatusChanged);
        }
    }
}
