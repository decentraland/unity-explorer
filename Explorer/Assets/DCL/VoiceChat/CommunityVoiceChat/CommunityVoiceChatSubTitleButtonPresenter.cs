using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Utilities;
using System;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    public class CommunityVoiceChatSubTitleButtonPresenter : IDisposable
    {
        private readonly IDisposable? statusSubscription;
        private readonly IDisposable? currentChannelSubscription;

        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly JoinCommunityLiveStreamChatSubTitleButtonView view;
        private readonly ICommunityCallOrchestrator communityCallOrchestrator;
        private readonly IReadonlyReactiveProperty<ChatChannel> currentChannel;
        private readonly bool disabled;

        private CancellationTokenSource communityCts = new ();
        private IDisposable? currentCommunityCallStatusSubscription;

        private bool canBeVisible;

        public CommunityVoiceChatSubTitleButtonPresenter(
            JoinCommunityLiveStreamChatSubTitleButtonView view,
            ICommunityCallOrchestrator communityCallOrchestrator,
            IReadonlyReactiveProperty<ChatChannel> currentChannel,
            CommunitiesDataProvider communityDataProvider)
        {
            this.view = view;
            this.communityCallOrchestrator = communityCallOrchestrator;
            this.currentChannel = currentChannel;
            this.communityDataProvider = communityDataProvider;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.VOICE_CHAT))
            {
                currentChannelSubscription = currentChannel.Subscribe(OnCurrentChannelChanged);
                statusSubscription = communityCallOrchestrator.CommunityCallStatus.Subscribe(OnCommunityCallStatusChanged);
                view.JoinStreamButton.onClick.AddListener(OnJoinStreamButtonClicked);
                disabled = false;
                canBeVisible = true;
            }
            else
                disabled = true;

            view.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            communityCts.SafeCancelAndDispose();

            if (disabled) return;

            statusSubscription?.Dispose();
            currentChannelSubscription?.Dispose();
            currentCommunityCallStatusSubscription?.Dispose();
            view.JoinStreamButton.onClick.RemoveListener(OnJoinStreamButtonClicked);
        }

        private void OnJoinStreamButtonClicked()
        {
            string communityId = ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id);
            communityCallOrchestrator.JoinCommunityVoiceChat(communityId, true);
        }

        private void OnCurrentChannelChanged(ChatChannel newChannel)
        {
            Reset();

            if (newChannel.ChannelType == ChatChannel.ChatChannelType.COMMUNITY)
                HandleChangeToCommunityChannelAsync(ChatChannel.GetCommunityIdFromChannelId(newChannel.Id)).Forget();
        }

        private void Reset()
        {
            communityCts = communityCts.SafeRestart();
            view.gameObject.SetActive(false);
            currentCommunityCallStatusSubscription?.Dispose();
        }

        private async UniTaskVoid HandleChangeToCommunityChannelAsync(string communityId)
        {
            currentCommunityCallStatusSubscription = communityCallOrchestrator.CommunityConnectionUpdates(communityId).Subscribe(OnCurrentCommunityCallStatusChanged);

            //We add a small delay to avoid UI issues when switching from window to panel
            await UniTask.Delay(1000, cancellationToken: communityCts.Token);

            //We subscribe to the call events but if the button cant be visible we don't need to check further.
            if (!canBeVisible || communityCts.IsCancellationRequested) return;

            try
            {
                GetCommunityResponse communityData = await communityDataProvider.GetCommunityAsync(communityId, communityCts.Token);
                GetCommunityResponse.VoiceChatStatus dataVoiceChatStatus = communityData.data.voiceChatStatus;

                if (!dataVoiceChatStatus.isActive)
                {
                    view.gameObject.SetActive(false);
                    return;
                }

                bool isOurCurrentConversation = communityCallOrchestrator.IsEqualToCurrentStreamingCommunity(communityId);

                if (!isOurCurrentConversation)
                {
                    view.gameObject.SetActive(dataVoiceChatStatus.isActive);
                    view.ParticipantsAmount.SetText(dataVoiceChatStatus.participantCount.ToString());
                }
            }
            catch (OperationCanceledException)
            {
                view.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                view.gameObject.SetActive(false);
                ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
            }
        }

        private void OnCurrentCommunityCallStatusChanged(bool hasActiveCall)
        {
            if (!hasActiveCall ||
                communityCallOrchestrator.IsEqualToCurrentStreamingCommunity(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id)))
            {
                view.gameObject.SetActive(false);
                return;
            }

            //If the panel can't be visible, we don't activate it.
            if (!canBeVisible) return;

            view.gameObject.SetActive(true);
            int participantsCount = communityCallOrchestrator.ParticipantsStateService.ConnectedParticipants.Count + 1;
            view.ParticipantsAmount.SetText(participantsCount.ToString());
        }

        private void OnCommunityCallStatusChanged(VoiceChatStatus status)
        {
            switch (status)
            {
                // If we just ended a call, we need to re-check the call status, etc., in case we need to show the button
                case VoiceChatStatus.DISCONNECTED or VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR or VoiceChatStatus.VOICE_CHAT_ENDING_CALL:
                    OnCurrentChannelChanged(currentChannel.Value);
                    break;

                // When we join a call, if it is for THIS community, we need to hide the button. If it's another community's call, we keep it.
                case VoiceChatStatus.VOICE_CHAT_STARTING_CALL or  VoiceChatStatus.VOICE_CHAT_IN_CALL when
                    communityCallOrchestrator.IsEqualToCurrentStreamingCommunity(ChatChannel.GetCommunityIdFromChannelId(currentChannel.Value.Id)):
                    view.gameObject.SetActive(false);
                    break;
            }
        }

        public void Hide()
        {
            if (disabled) return;
            if (!canBeVisible) return;

            canBeVisible = false;
            view.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (disabled) return;
            if (canBeVisible) return;

            canBeVisible = true;
            OnCurrentChannelChanged(currentChannel.Value);
        }

        public void SetFocusState(bool isFocused, bool animate, float duration)
        {
            view.SetFocusedState(isFocused, animate, duration);
        }
    }
}
