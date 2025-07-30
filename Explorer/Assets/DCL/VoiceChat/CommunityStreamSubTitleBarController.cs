using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityStreamSubTitleBarController : IDisposable
    {
        private readonly IDisposable statusSubscription;
        private readonly IDisposable currentChannelSubscription;

        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly CommunityStreamSubTitleBarView view;
        private readonly IVoiceChatOrchestrator orchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;

        private readonly CommunityStreamJoinButtonController joinButtonController;
        private CancellationTokenSource communityCts = new ();
        private bool isVoiceChatActive;
        private bool isCurrentCall;
        private IDisposable currentCommunityCallStatusSubscription;

        public CommunityStreamSubTitleBarController(
            CommunityStreamSubTitleBarView view,
            IVoiceChatOrchestrator orchestrator,
            IReadonlyReactiveProperty<ChatChannel> currentChannel,
            CommunitiesDataProvider communityDataProvider)
        {
            this.view = view;
            this.orchestrator = orchestrator;
            this.currentChannel = currentChannel;
            this.communityDataProvider = communityDataProvider;

            joinButtonController = new CommunityStreamJoinButtonController(
                view.JoinStreamButton,
                orchestrator,
                currentChannel,
                communityDataProvider);

            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
            statusSubscription = orchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            joinButtonController?.Dispose();
            communityCts?.Dispose();
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status != VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            if (!view.gameObject.activeSelf) return;

            if (orchestrator.CurrentCallId == ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id))
            {
                //If it's the current call, we can get the call information directly from the orchestrator
                isCurrentCall = true;
                HandleCurrentCommunityCall();
            }
            else { view.gameObject.SetActive(false); }
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            //We hide it by default until we resolve if the user should see it.
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
        }

        private async UniTaskVoid HandleChangeToCommunityChannelAsync(string communityId)
        {
            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);
            bool isVoiceChatActive = communityData.data.voiceChatStatus.isActive;


            currentCommunityCallStatusSubscription = orchestrator.CommunityStatusService.SubscribeToCommunityUpdates(communityId)?.Subscribe(OnCurrentCommunityCallStatusChanged);

            //If there is no voice chat active, we just don't show this.
            if (!isVoiceChatActive) return;

            view.gameObject.SetActive(true);

            if (orchestrator.CurrentCallId == communityId)
            {
                //If it's the current call, we can get the call information directly from the orchestrator
                isCurrentCall = true;
                HandleCurrentCommunityCall();
            }
            else
            {
                //If it's not the current call, we need to get the call information from the communities data
                isCurrentCall = false;
                HandleOtherCommunityCallAsync(communityId).Forget();
            }

        }

        private void OnCurrentCommunityCallStatusChanged(bool hasActiveCall)
        {
            //We show the button if the current community has an active call
            view.gameObject.SetActive(hasActiveCall);

            if (hasActiveCall && orchestrator.CurrentCallId == ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id))
                HandleCurrentCommunityCall();
        }


        private void HandleCurrentCommunityCall()
        {
            view.gameObject.SetActive(true);
            int participantsCount = orchestrator.ParticipantsStateService.ConnectedParticipants.Count;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
            view.JoinStreamButton.gameObject.SetActive(false);
            view.InStreamSign.SetActive(true);
        }

        private async UniTaskVoid HandleOtherCommunityCallAsync(string communityId)
        {
            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);

            int participantsCount = communityData.data.voiceChatStatus.moderatorCount + communityData.data.voiceChatStatus.participantCount;
            view.ParticipantsAmount.SetText(participantsCount.ToString());

            view.InStreamSign.SetActive(true);
            view.JoinStreamButton.gameObject.SetActive(true);

            // it will show tooltip saying we are already in a call.
            // Otherwise, we will join the call.
        }
    }
}
