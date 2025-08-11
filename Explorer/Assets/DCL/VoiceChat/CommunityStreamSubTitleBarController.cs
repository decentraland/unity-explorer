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
    public class CommunityStreamSubTitleBarController : IDisposable
    {
        private const string TAG = nameof(CommunityStreamSubTitleBarController);

        private readonly IDisposable statusSubscription;
        private readonly IDisposable currentChannelSubscription;

        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly CommunityStreamSubTitleBarView view;
        private readonly ICommunityCallOrchestrator communityCallOrchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;

        private readonly CommunityStreamJoinButtonController joinButtonController;
        private CancellationTokenSource communityCts = new ();
        private IDisposable currentCommunityCallStatusSubscription;
        private bool isCurrentCall;
        private bool isMemberListVisible;

        public CommunityStreamSubTitleBarController(
            CommunityStreamSubTitleBarView view,
            ICommunityCallOrchestrator communityCallOrchestrator,
            IReadonlyReactiveProperty<ChatChannel> currentChannel,
            CommunitiesDataProvider communityDataProvider)
        {
            this.view = view;
            this.communityCallOrchestrator = communityCallOrchestrator;
            this.currentChannel = currentChannel;
            this.communityDataProvider = communityDataProvider;

            joinButtonController = new CommunityStreamJoinButtonController(
                view.JoinStreamButton,
                communityCallOrchestrator,
                currentChannel);

            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
            statusSubscription = communityCallOrchestrator.CommunityCallStatus.Subscribe(OnCommunityCallStatusChanged);
        }

        private void ParticipantsStateServiceOnParticipantLeft(string _)
        {
            if (!isCurrentCall) return;

            SetParticipantsCount();
        }

        private void ParticipantsStateServiceOnParticipantJoined(string _, VoiceChatParticipantsStateService.ParticipantState __)
        {
            if (!isCurrentCall) return;

            SetParticipantsCount();
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            currentCommunityCallStatusSubscription?.Dispose();
            joinButtonController?.Dispose();
            communityCts?.SafeCancelAndDispose();
            communityCallOrchestrator.ParticipantsStateService.ParticipantJoined -= ParticipantsStateServiceOnParticipantJoined;
            communityCallOrchestrator.ParticipantsStateService.ParticipantLeft -= ParticipantsStateServiceOnParticipantLeft;
        }

        private void OnCommunityCallStatusChanged(VoiceChatStatus status)
        {
            if (isMemberListVisible) return;

            if (status != VoiceChatStatus.VOICE_CHAT_IN_CALL)
            {
                if (status is VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR)
                {
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCommunityCallStatusChanged: Call ended with status {status}, triggering channel change");
                    OnCurrentChannelChanged(currentChannel.Value);
                    return;
                }
            }

            if (communityCallOrchestrator.CurrentCommunityId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), StringComparison.InvariantCultureIgnoreCase))
            {
                //If it's the current call, we can get the call information directly from the orchestrator
                HandleCurrentCommunityCall();
            }
            else
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} OnCommunityCallStatusChanged: Hiding subtitle bar - not current community call {ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id)} - orchestrator communityID: {communityCallOrchestrator.CurrentCommunityId.Value}");
                view.gameObject.SetActive(false);
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
            communityCallOrchestrator.ParticipantsStateService.ParticipantJoined -= ParticipantsStateServiceOnParticipantJoined;
            communityCallOrchestrator.ParticipantsStateService.ParticipantLeft -= ParticipantsStateServiceOnParticipantLeft;

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

            isCurrentCall = false;
            bool isVoiceChatActive = communityCallOrchestrator.HasActiveVoiceChatCall(communityId);

            currentCommunityCallStatusSubscription = communityCallOrchestrator.SubscribeToCommunityUpdates(communityId)?.Subscribe(OnCurrentCommunityCallStatusChanged);

            //If there is no voice chat active, we just don't show this.
            if (!isVoiceChatActive)
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: No voice chat active for community {communityId}, keeping subtitle bar hidden");
                return;
            }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleChangeToCommunityChannelAsync: Showing subtitle bar for community {communityId} with active voice chat");
            view.gameObject.SetActive(true);

            if (communityCallOrchestrator.CurrentCommunityId.Value.Equals(communityId, StringComparison.InvariantCultureIgnoreCase))
            {
                //If it's the current call, we can get the call information directly from the orchestrator
                HandleCurrentCommunityCall();
            }
            else
            {
                //If it's not the current call, we need to get the call information from the communities data
                HandleOtherCommunityCallAsync(communityId).Forget();
            }
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

            isCurrentCall = true;
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} HandleCurrentCommunityCall: Setting up subtitle bar for current community call");
            view.gameObject.SetActive(true);
            SetParticipantsCount();
            view.JoinStreamButton.gameObject.SetActive(false);
            view.InStreamSign.SetActive(true);

            communityCallOrchestrator.ParticipantsStateService.ParticipantJoined += ParticipantsStateServiceOnParticipantJoined;
            communityCallOrchestrator.ParticipantsStateService.ParticipantLeft += ParticipantsStateServiceOnParticipantLeft;
        }

        private void SetParticipantsCount()
        {
            int participantsCount = communityCallOrchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
        }

        private async UniTaskVoid HandleOtherCommunityCallAsync(string communityId)
        {
            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);

            int participantsCount = communityData.data.voiceChatStatus.participantCount;
            view.ParticipantsAmount.SetText(participantsCount.ToString());

            view.InStreamSign.SetActive(false);
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
                // This will trigger the normal flow to determine if it should be shown
                OnCurrentChannelChanged(currentChannel.Value);
            }
        }
    }
}
