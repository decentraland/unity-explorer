using DCL.NotificationsBusController.NotificationTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace DCL.Notifications.Serialization
{
    [Preserve]
    public class NotificationJsonDtoConverter : JsonConverter<List<INotification>>
    {
        private const string EVENT_STARTED_TYPE = "events_started";
        private const string EVENT_ENDED_TYPE = "events_ended";
        private const string REWARD_ASSIGNED_TYPE = "reward_assignment";
        private const string REWARD_IN_PROGRESS_TYPE = "reward_in_progress";
        private const string BADGE_GRANTED_TYPE = "badge_granted";
        private const string FRIENDSHIP_RECEIVED_TYPE = "social_service_friendship_request";
        private const string FRIENDSHIP_ACCEPTED_TYPE = "social_service_friendship_accepted";
        private const string MARKETPLACE_CREDITS_TYPE = "credits_goal_completed";
        private const string STREAMING_KEY_RESET = "streaming_key_reset";
        private const string STREAMING_KEY_REVOKE = "streaming_key_revoke";
        private const string STREAMING_KEY_EXPIRED = "streaming_key_expired";
        private const string STREAMING_TIME_EXCEEDED = "streaming_time_exceeded";
        private const string STREAMING_PLACE_UPDATED = "streaming_place_updated";
        private const string REFERRAL_INVITED_USERS_ACCEPTED = "referral_invited_users_accepted";
        private const string REFERRAL_NEW_TIER_REACHED = "referral_new_tier_reached";
        private const string EVENT_CREATED_TYPE = "event_created";
        private const string COMMUNITY_MEMBER_REMOVED_TYPE = "community_member_removed";
        private const string COMMUNITY_MEMBER_BANNED_TYPE = "community_member_banned";
        private const string COMMUNITY_RENAMED_TYPE = "community_renamed";
        private const string COMMUNITY_DELETED_TYPE = "community_deleted";
        private const string COMMUNITY_REQUEST_TO_JOIN_RECEIVED_TYPE = "community_request_to_join_received";
        private const string COMMUNITY_INVITE_RECEIVED_TYPE = "community_invite_received";
        private const string COMMUNITY_REQUEST_TO_JOIN_ACCEPTED_TYPE = "community_request_to_join_accepted";

        private static readonly JArray EMPTY_J_ARRAY = new ();

        private readonly List<string> excludedTypes = new ();

        public NotificationJsonDtoConverter(bool includeFriendsNotifications)
        {
            if (!includeFriendsNotifications)
            {
                excludedTypes.Add(FRIENDSHIP_RECEIVED_TYPE);
                excludedTypes.Add(FRIENDSHIP_ACCEPTED_TYPE);
            }
        }

        public override void WriteJson(JsonWriter writer, List<INotification>? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<INotification>? ReadJson(JsonReader reader, Type objectType, List<INotification>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var notificationsList = JObject.Load(reader).Value<JArray>("notifications") ?? EMPTY_J_ARRAY;
            existingValue ??= new List<INotification>();

            foreach (var item in notificationsList)
            {
                if (item is not JObject notification)
                    continue;

                string? type = notification["type"]?.Value<string>();
                if (type == null)
                    continue;

                if (excludedTypes.Contains(type))
                    continue;

                INotification? notificationObject = type switch
                {
                    EVENT_STARTED_TYPE => new EventStartedNotification(),
                    EVENT_ENDED_TYPE => new EventEndedNotification(),
                    REWARD_ASSIGNED_TYPE => new RewardAssignedNotification(),
                    REWARD_IN_PROGRESS_TYPE => new RewardInProgressNotification(),
                    BADGE_GRANTED_TYPE => new BadgeGrantedNotification(),
                    FRIENDSHIP_RECEIVED_TYPE => new FriendRequestReceivedNotification(),
                    FRIENDSHIP_ACCEPTED_TYPE => new FriendRequestAcceptedNotification(),
                    MARKETPLACE_CREDITS_TYPE => new MarketplaceCreditsNotification(),
                    STREAMING_KEY_RESET => new StreamingFeatureNotification(),
                    STREAMING_KEY_REVOKE => new StreamingFeatureNotification(),
                    STREAMING_KEY_EXPIRED => new StreamingFeatureNotification(),
                    STREAMING_TIME_EXCEEDED => new StreamingFeatureNotification(),
                    STREAMING_PLACE_UPDATED => new StreamingFeatureNotification(),
                    REFERRAL_INVITED_USERS_ACCEPTED => new ReferralNotification(NotificationType.REFERRAL_INVITED_USERS_ACCEPTED),
                    REFERRAL_NEW_TIER_REACHED => new ReferralNotification(NotificationType.REFERRAL_NEW_TIER_REACHED),
                    EVENT_CREATED_TYPE => new CommunityEventCreatedNotification(),
                    COMMUNITY_MEMBER_REMOVED_TYPE => new CommunityUserRemovedNotification(),
                    COMMUNITY_MEMBER_BANNED_TYPE => new CommunityUserBannedNotification(),
                    COMMUNITY_RENAMED_TYPE => new CommunityRenamedNotification(),
                    COMMUNITY_DELETED_TYPE => new CommunityDeletedNotification(),
                    COMMUNITY_REQUEST_TO_JOIN_RECEIVED_TYPE => new CommunityUserRequestToJoinNotification(),
                    COMMUNITY_INVITE_RECEIVED_TYPE => new CommunityUserInvitedNotification(),
                    COMMUNITY_REQUEST_TO_JOIN_ACCEPTED_TYPE => new CommunityUserRequestToJoinAcceptedNotification(),
                    _ => null,
                };

                if (notificationObject == null)
                    continue;

                serializer.Populate(notification.CreateReader(), notificationObject);
                existingValue.Add(notificationObject);
            }

            return existingValue;
        }
    }
}
