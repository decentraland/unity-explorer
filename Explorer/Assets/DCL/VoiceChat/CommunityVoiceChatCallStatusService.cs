using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.VoiceChat.Services;
using Decentraland.SocialService.V2;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Implementation of voice chat call status service for community calls.
    /// Currently provides empty implementations as community voice chat is not yet implemented.
    /// </summary>
    public class CommunityVoiceChatCallStatusService : VoiceChatCallStatusServiceBase, ICommunityVoiceChatCallStatusService
    {
        private readonly ICommunityVoiceService voiceChatService;
        private CancellationTokenSource cts;
        private readonly Dictionary<string, string> communityVoiceChatCalls = new();
        private readonly Dictionary<string, CommunitySubscription> communitySubscriptions = new();

        public CommunityVoiceChatCallStatusService(ICommunityVoiceService voiceChatService)
        {
            this.voiceChatService = voiceChatService;
            this.voiceChatService.CommunityVoiceChatUpdateReceived += OnCommunityVoiceChatUpdateReceived;
        }

        public override void StartCall(string communityId)
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status.Value is not VoiceChatStatus.DISCONNECTED and not VoiceChatStatus.VOICE_CHAT_BUSY and not VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR) return;

            cts = cts.SafeRestart();

            //Setting starting call status to instantly disable call button
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTING_CALL);

            StartCallAsync(communityId, cts.Token).Forget();
        }

        private async UniTaskVoid StartCallAsync(string communityId, CancellationToken ct)
        {
            try
            {
                StartCommunityVoiceChatResponse response = await voiceChatService.StartCommunityVoiceChatAsync(communityId, ct);

                switch (response.ResponseCase)
                {
                    //When the call can be started
                    case StartCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        RoomUrl = response.Ok.Credentials.ConnectionUrl;
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;

                    case StartCommunityVoiceChatResponse.ResponseOneofCase.InvalidRequest:
                    case StartCommunityVoiceChatResponse.ResponseOneofCase.ConflictingError:
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_BUSY);
                        break;
                    default:
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
            }
        }

        public override void HangUp()
        {
            //TODO: currently just exits, need to figure out how to handle hang up
            UpdateStatus(VoiceChatStatus.DISCONNECTED);
        }

        public void JoinCommunityVoiceChat()
        {
            //We can start a call only if we are not connected or trying to start a call
            if (Status.Value is not VoiceChatStatus.DISCONNECTED and not VoiceChatStatus.VOICE_CHAT_BUSY and not VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR) return;

            cts = cts.SafeRestart();
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_STARTED_CALL);
            JoinCommunityVoiceChatAsync(CallId, cts.Token).Forget();
        }

        public async UniTaskVoid JoinCommunityVoiceChatAsync(string communityId, CancellationToken ct)
        {
            try
            {
                JoinCommunityVoiceChatResponse response = await voiceChatService.JoinCommunityVoiceChatAsync(communityId, ct);

                switch (response.ResponseCase)
                {
                    case JoinCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        RoomUrl = response.Ok.Credentials.ConnectionUrl;
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_IN_CALL);
                        break;
                    default:
                        ResetVoiceChatData();
                        UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
                        break;
                }
            }
            catch (Exception e)
            {
            }
        }

        public void RequestToSpeak(string communityId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            RequestToSpeakAsync(communityId, cts.Token).Forget();
        }

        private async UniTaskVoid RequestToSpeakAsync(string communityId, CancellationToken ct)
        {
            try
            {
                RequestToSpeakInCommunityVoiceChatResponse response = await voiceChatService.RequestToSpeakInCommunityVoiceChatAsync(communityId, ct);

                switch (response.ResponseCase)
                {
                    case RequestToSpeakInCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        //Handle raise hand logic
                        break;
                }
            }
            catch (Exception e)
            {
            }
        }

        public void PromoteToSpeaker(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            PromoteToSpeakerAsync(communityId, walletId, cts.Token).Forget();
        }

        private async UniTaskVoid PromoteToSpeakerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                PromoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.PromoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                switch (response.ResponseCase)
                {
                    case PromoteSpeakerInCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        //Handle promote logic here
                        break;
                }
            }
            catch (Exception e)
            {
            }
        }

        public void DemoteFromSpeaker(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            DemoteFromSpeakerAsync(communityId, walletId, cts.Token).Forget();
        }

        private async UniTaskVoid DemoteFromSpeakerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                DemoteSpeakerInCommunityVoiceChatResponse response = await voiceChatService.DemoteSpeakerInCommunityVoiceChatAsync(communityId, walletId, ct);

                switch (response.ResponseCase)
                {
                    case DemoteSpeakerInCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        //Handle demote logic here
                        break;
                }
            }
            catch (Exception e)
            {
            }
        }

        public void KickPlayer(string communityId, string walletId)
        {
            if (Status.Value is not VoiceChatStatus.VOICE_CHAT_IN_CALL) return;

            cts = cts.SafeRestart();
            KickPlayerAsync(communityId, walletId, cts.Token).Forget();
        }

        private async UniTaskVoid KickPlayerAsync(string communityId, string walletId, CancellationToken ct)
        {
            try
            {
                KickPlayerFromCommunityVoiceChatResponse response = await voiceChatService.KickPlayerFromCommunityVoiceChatAsync(communityId, walletId, ct);

                switch (response.ResponseCase)
                {
                    case KickPlayerFromCommunityVoiceChatResponse.ResponseOneofCase.Ok:
                        //Handle kick logic
                        break;
                }
            }
            catch (Exception e)
            {
            }
        }

        public override void HandleLivekitConnectionFailed()
        {
            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, "Community voice chat HandleLivekitConnectionFailed not yet implemented");
            UpdateStatus(VoiceChatStatus.VOICE_CHAT_GENERIC_ERROR);
        }

        private void OnCommunityVoiceChatUpdateReceived(CommunityVoiceChatUpdate communityUpdate)
        {
            if (string.IsNullOrEmpty(communityUpdate.CommunityId))
            {
                ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, "Received community voice chat update with empty community ID");
                return;
            }

            if (string.IsNullOrEmpty(communityUpdate.VoiceChatId))
            {
                // Remove community from dictionary if call_id is empty
                if (communityVoiceChatCalls.Remove(communityUpdate.CommunityId))
                {
                    ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Removed community {communityUpdate.CommunityId} from voice chat calls");
                }
            }
            else
            {
                // Add or update community with new call_id
                communityVoiceChatCalls[communityUpdate.CommunityId] = communityUpdate.VoiceChatId;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Updated community {communityUpdate.CommunityId} with Voice Chat ID {communityUpdate.VoiceChatId}");
            }

            // Notify subscribers for this specific community
            if (communitySubscriptions.TryGetValue(communityUpdate.CommunityId, out var subscription))
            {
                subscription.Property.Value = new CommunityCallStatus(communityUpdate.VoiceChatId);
            }
        }

        private async UniTaskVoid SubscribeToCommunityVoiceChatUpdatesAsync(CancellationToken ct)
        {
            try
            {
                await voiceChatService.SubscribeToCommunityVoiceChatUpdatesAsync(ct)
                                           .SuppressToResultAsync(ReportCategory.COMMUNITY_VOICE_CHAT);
            }
            catch (OperationCanceledException) { } // Expected, don't report
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITY_VOICE_CHAT));
            }
        }

        public override void Dispose()
        {
            if (voiceChatService != null)
            {
                voiceChatService.CommunityVoiceChatUpdateReceived -= OnCommunityVoiceChatUpdateReceived;
                voiceChatService.Dispose();
            }

            foreach (var subscription in communitySubscriptions.Values)
            {
                subscription.Property.Dispose();
            }
            communitySubscriptions.Clear();

            cts.SafeCancelAndDispose();
            base.Dispose();
        }

        public bool HasActiveVoiceChatCall(string communityId, out string? callId)
        {
            if (string.IsNullOrEmpty(communityId))
            {
                callId = null;
                return false;
            }

            return communityVoiceChatCalls.TryGetValue(communityId, out callId);
        }

        public IReadonlyReactiveProperty<CommunityCallStatus>? SubscribeToCommunityUpdates(string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return null;

            if (communitySubscriptions.TryGetValue(communityId, out var existingSubscription))
            {
                var updatedSubscription = existingSubscription.WithIncrementedCount();
                communitySubscriptions[communityId] = updatedSubscription;
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Added subscriber to community {communityId}, total subscribers: {updatedSubscription.SubscriberCount}");
                return existingSubscription.Property;
            }

            var newProperty = new ReactiveProperty<CommunityCallStatus>(CommunityCallStatus.NoCall);
            communitySubscriptions[communityId] = new CommunitySubscription(newProperty, 1);

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Created subscription for community {communityId}");
            return newProperty;
        }

        public void UnsubscribeFromCommunityUpdates(string communityId)
        {
            if (string.IsNullOrEmpty(communityId))
                return;

            if (communitySubscriptions.TryGetValue(communityId, out var subscription))
            {
                var updatedSubscription = subscription.WithDecrementedCount();

                if (updatedSubscription.HasNoSubscribers)
                {
                    // No more subscribers, dispose and remove the property
                    subscription.Property.Dispose();
                    communitySubscriptions.Remove(communityId);
                }
                else
                {
                    // Decrement subscriber count but keep the property
                    communitySubscriptions[communityId] = updatedSubscription;
                }

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"Removed subscriber from community {communityId}, remaining subscribers: {updatedSubscription.SubscriberCount}");
            }
        }

        /// <summary>
        /// Encapsulates a reactive property with its subscriber count for community voice chat updates
        /// </summary>
        private readonly struct CommunitySubscription
        {
            public readonly ReactiveProperty<CommunityCallStatus> Property;
            public readonly int SubscriberCount;

            public CommunitySubscription(ReactiveProperty<CommunityCallStatus> property, int subscriberCount)
            {
                Property = property;
                SubscriberCount = subscriberCount;
            }

            public CommunitySubscription WithIncrementedCount() => new(Property, SubscriberCount + 1);
            public CommunitySubscription WithDecrementedCount() => new(Property, SubscriberCount - 1);
            public bool HasNoSubscribers => SubscriberCount <= 0;
        }
    }
}
