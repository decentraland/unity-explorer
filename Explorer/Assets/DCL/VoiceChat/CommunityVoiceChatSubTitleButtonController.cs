using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityVoiceChatSubTitleButtonController : IDisposable
    {
        private const string TAG = nameof(CommunityVoiceChatSubTitleButtonController);

        private readonly IDisposable statusSubscription;
        private readonly IDisposable currentChannelSubscription;

        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly CommunityStreamSubTitleButtonView view;
        private readonly ICommunityCallOrchestrator communityCallOrchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;

        private CancellationTokenSource communityCts = new ();
        private CancellationTokenSource joinCallCts = new ();
        private IDisposable? currentCommunityCallStatusSubscription;

        private bool isMemberListVisible;

        public CommunityVoiceChatSubTitleButtonController(
            CommunityStreamSubTitleButtonView view,
            ICommunityCallOrchestrator communityCallOrchestrator,
            IReadonlyReactiveProperty<ChatChannel> currentChannel,
            CommunitiesDataProvider communityDataProvider)
        {
            this.view = view;
            this.communityCallOrchestrator = communityCallOrchestrator;
            this.currentChannel = currentChannel;
            this.communityDataProvider = communityDataProvider;

            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
            statusSubscription = communityCallOrchestrator.CommunityCallStatus.Subscribe(OnCommunityCallStatusChanged);
            view.JoinStreamButton.onClick.AddListener(OnJoinStreamButtonClicked);


        }

        private void OnJoinStreamButtonClicked()
        {
            joinCallCts = joinCallCts.SafeRestart();
            string communityId = ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id);
            communityCallOrchestrator.JoinCommunityVoiceChat(communityId, joinCallCts.Token, true);
        }

        public void Dispose()
        {
            statusSubscription.Dispose();
            currentChannelSubscription.Dispose();
            currentCommunityCallStatusSubscription?.Dispose();
            communityCts.SafeCancelAndDispose();
            view.JoinStreamButton.onClick.RemoveListener(OnJoinStreamButtonClicked);
        }

        private void OnCommunityCallStatusChanged(VoiceChatStatus status)
        {
            if (isMemberListVisible) return;

            switch (status)
            {
                // If we just ended a call, we need to re-check the call status, etc., in case we need to show the button.
                case VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCommunityCallStatusChanged: Call ended with status {status}, triggering channel change");
                    OnCurrentChannelChanged(currentChannel.Value);
                    break;
                // When we join a call, if it is for THIS community, we need to hide the button. If it's another call, we keep it.
                case VoiceChatStatus.VOICE_CHAT_IN_CALL when
                    communityCallOrchestrator.CurrentCommunityId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), StringComparison.InvariantCultureIgnoreCase):
                    view.gameObject.SetActive(false);
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCommunityCallStatusChanged: Hiding subtitle bar, joined current call");
                    break;
            }
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            //We hide it by default until we resolve if the user should see it.
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCurrentChannelChanged: Hiding subtitle bar by default for channel type {newChannel.ChannelType}");
            view.gameObject.SetActive(false);

            // Reset member list visibility state since we're changing channels
            isMemberListVisible = false;

            currentCommunityCallStatusSubscription?.Dispose();

            switch (newChannel.ChannelType)
            {
                case ChatChannel.ChatChannelType.COMMUNITY:
                    HandleChangeToCommunityChannel(ChatChannel.GetCommunityIdFromChannelId(newChannel.Id));
                    break;
                case ChatChannel.ChatChannelType.NEARBY:
                case ChatChannel.ChatChannelType.USER:
                case ChatChannel.ChatChannelType.UNDEFINED:
                    break;
            }
        }

        private void HandleChangeToCommunityChannel(string communityId)
        {
            if (isMemberListVisible) return;

            bool isVoiceChatActive = communityCallOrchestrator.HasActiveVoiceChatCall(communityId);

            currentCommunityCallStatusSubscription = communityCallOrchestrator.SubscribeToCommunityUpdates(communityId).Subscribe(OnCurrentCommunityCallStatusChanged);

            // If there is no voice chat active, we just don't show this.
            if (!isVoiceChatActive)
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: No voice chat active for community {communityId}, keeping subtitle bar hidden");
                return;
            }

            // If it's the current community call, we don't show the subtitle.
            if (communityCallOrchestrator.CurrentCommunityId.Value.Equals(communityId, StringComparison.InvariantCultureIgnoreCase)) return;


            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: Showing subtitle bar for community {communityId} with active voice chat");
            view.gameObject.SetActive(true);
            //If it's not the current call, we need to get the call information from the communities data
            HandleCommunityCallAsync(communityId).Forget();
        }

        private void OnCurrentCommunityCallStatusChanged(bool hasActiveCall)
        {
            if (isMemberListVisible) return;

            //We show the button if the current community has an active call
            if (hasActiveCall)
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCurrentCommunityCallStatusChanged: Showing subtitle bar - community has active call");
                view.gameObject.SetActive(true);
            }

            if (hasActiveCall && communityCallOrchestrator.CurrentCommunityId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), StringComparison.InvariantCultureIgnoreCase))
                HandleCurrentCommunityCall();
        }


        private void HandleCurrentCommunityCall()
        {
            if (isMemberListVisible) return;

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleCurrentCommunityCall: Setting up subtitle bar for current community call");
            view.gameObject.SetActive(true);
            SetParticipantsCount();
            view.JoinStreamButton.gameObject.SetActive(false);
        }

        private void SetParticipantsCount()
        {
            int participantsCount = communityCallOrchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
        }

        private async UniTaskVoid HandleCommunityCallAsync(string communityId)
        {
            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);

            int participantsCount = communityData.data.voiceChatStatus.participantCount;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
            view.JoinStreamButton.gameObject.SetActive(true);
        }

        public void OnMemberListVisibilityChanged(bool isVisible)
        {
            isMemberListVisible = isVisible;

            if (isVisible)
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnMemberListVisibilityChanged: Hiding subtitle bar - member list is visible");
                view.gameObject.SetActive(false);
            }
            else
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnMemberListVisibilityChanged: Member list hidden, re-evaluating subtitle bar visibility");
                // Re-setup subtitle bar when member list becomes hidden
                // This will trigger the normal flow to determine if it should be shown or not
                OnCurrentChannelChanged(currentChannel.Value);
            }
        }
    }
}
