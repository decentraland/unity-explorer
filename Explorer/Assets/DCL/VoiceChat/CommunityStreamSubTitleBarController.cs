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
        private readonly ICommunityCallOrchestrator orchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;

        private readonly CommunityStreamJoinButtonController joinButtonController;
        private CancellationTokenSource communityCts = new ();
        private IDisposable currentCommunityCallStatusSubscription;
        private bool isCurrentCall;

        public CommunityStreamSubTitleBarController(
            CommunityStreamSubTitleBarView view,
            ICommunityCallOrchestrator orchestrator,
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
                currentChannel);

            currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
            statusSubscription = orchestrator.CurrentCallStatus.Subscribe(OnCallStatusChanged);
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
            orchestrator.ParticipantsStateService.ParticipantJoined -= ParticipantsStateServiceOnParticipantJoined;
            orchestrator.ParticipantsStateService.ParticipantLeft -= ParticipantsStateServiceOnParticipantLeft;
        }

        private void OnCallStatusChanged(VoiceChatStatus status)
        {
            if (status != VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            if (!view.gameObject.activeSelf) return;

            if (orchestrator.CurrentCommunityId == ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id))
            {
                //If it's the current call, we can get the call information directly from the orchestrator
                HandleCurrentCommunityCall();
            }
            else { view.gameObject.SetActive(false); }
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            //We hide it by default until we resolve if the user should see it.
            view.gameObject.SetActive(false);
            currentCommunityCallStatusSubscription?.Dispose();
            orchestrator.ParticipantsStateService.ParticipantJoined -= ParticipantsStateServiceOnParticipantJoined;
            orchestrator.ParticipantsStateService.ParticipantLeft -= ParticipantsStateServiceOnParticipantLeft;

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
            isCurrentCall = false;

            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);
            bool isVoiceChatActive = communityData.data.voiceChatStatus.isActive;

            currentCommunityCallStatusSubscription = orchestrator.SubscribeToCommunityUpdates(communityId)?.Subscribe(OnCurrentCommunityCallStatusChanged);

            //If there is no voice chat active, we just don't show this.
            if (!isVoiceChatActive) return;

            view.gameObject.SetActive(true);

            if (orchestrator.CurrentCommunityId == communityId)
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
            //We show the button if the current community has an active call
            view.gameObject.SetActive(hasActiveCall);

            if (hasActiveCall && orchestrator.CurrentCommunityId == ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id))
                HandleCurrentCommunityCall();
        }


        private void HandleCurrentCommunityCall()
        {
            isCurrentCall = true;
            view.gameObject.SetActive(true);
            SetParticipantsCount();
            view.JoinStreamButton.gameObject.SetActive(false);
            view.InStreamSign.SetActive(true);

            orchestrator.ParticipantsStateService.ParticipantJoined += ParticipantsStateServiceOnParticipantJoined;
            orchestrator.ParticipantsStateService.ParticipantLeft += ParticipantsStateServiceOnParticipantLeft;
        }

        private void SetParticipantsCount()
        {
            int participantsCount = orchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
        }

        private async UniTaskVoid HandleOtherCommunityCallAsync(string communityId)
        {
            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);

            int participantsCount = communityData.data.voiceChatStatus.participantCount;
            view.ParticipantsAmount.SetText(participantsCount.ToString());

            view.InStreamSign.SetActive(true);
            view.JoinStreamButton.gameObject.SetActive(true);
        }
    }
}
