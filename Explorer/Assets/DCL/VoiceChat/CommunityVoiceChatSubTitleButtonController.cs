using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities;
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

       private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            Reset();

            if (newChannel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                HandleChangeToCommunityChannelAsync(ChatChannel.GetCommunityIdFromChannelId(newChannel.Id)).Forget();
        }

        private void Reset()
        {
            view.gameObject.SetActive(false);
            isMemberListVisible = false;
            currentCommunityCallStatusSubscription?.Dispose();
            currentCommunityCallStatusSubscription = null;
        }

        private async UniTaskVoid HandleChangeToCommunityChannelAsync(string communityId)
        {
            currentCommunityCallStatusSubscription = communityCallOrchestrator.SubscribeToCommunityUpdates(communityId)?.Subscribe(OnCurrentCommunityCallStatusChanged);

            bool isOurCurrentConversation = communityCallOrchestrator.CurrentCommunityId.Value.Equals(communityId, StringComparison.InvariantCultureIgnoreCase);
            if (isOurCurrentConversation) return;

            communityCts = communityCts.SafeRestart();
            GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);

            var dataVoiceChatStatus = communityData.data.voiceChatStatus;

            bool isVoiceChatActive = dataVoiceChatStatus.isActive;
            if (!isVoiceChatActive) return;

            view.gameObject.SetActive(true);
            int participantsCount = dataVoiceChatStatus.participantCount;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
        }

        private void OnCurrentCommunityCallStatusChanged(bool hasActiveCall)
        {
            if (isMemberListVisible) return;

            if (communityCallOrchestrator.CurrentCommunityId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), StringComparison.InvariantCultureIgnoreCase) || !hasActiveCall)
            {
                view.gameObject.SetActive(false);
                return;
            }

            if (!hasActiveCall) return;

            view.gameObject.SetActive(true);
            int participantsCount = communityCallOrchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1;
            view.ParticipantsAmount.SetText(participantsCount.ToString());

        }

        private void OnCommunityCallStatusChanged(VoiceChatStatus status)
        {
            if (isMemberListVisible) return;

            switch (status)
            {
                // If we just ended a call, we need to re-check the call status, etc., in case we need to show the button
                case VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR:
                    OnCallEndedAsync().Forget();
                    break;
                // When we join a call, if it is for THIS community, we need to hide the button. If it's another call, we keep it.
                case VoiceChatStatus.VOICE_CHAT_IN_CALL when
                    communityCallOrchestrator.CurrentCommunityId.Value.Equals(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id), StringComparison.InvariantCultureIgnoreCase):
                    view.gameObject.SetActive(false);
                    break;
            }
        }

        private async UniTaskVoid OnCallEndedAsync()
        {
            // We wait for a short time before getting the updated data just because BE might take some time to update the call status.
            Reset();
            await UniTask.Delay(500);
            HandleChangeToCommunityChannelAsync(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id)).Forget();
        }

        public void OnMemberListVisibilityChanged(bool isVisible)
        {
            isMemberListVisible = isVisible;

            if (isVisible)
            {
                view.gameObject.SetActive(false);
            }
            else
            {
                // Re-setup subtitle bar when member list becomes hidden
                // This will trigger the normal flow to determine if it should be shown or not
                OnCurrentChannelChanged(currentChannel.Value);
            }
        }
    }
}
