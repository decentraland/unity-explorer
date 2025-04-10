using DCL.NotificationsBusController.NotificationTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace DCL.Notifications.Serialization
{
    public class NotificationJsonDtoConverter : JsonConverter<List<INotification>>
    {
        private const string FRIENDSHIP_RECEIVED_TYPE = "social_service_friendship_request";
        private const string FRIENDSHIP_ACCEPTED_TYPE = "social_service_friendship_accepted";
        private const string MARKETPLACE_CREDITS_TYPE = "marketplace_credits";

        private readonly List<string> excludedTypes = new ();

        public NotificationJsonDtoConverter(bool includeFriendsNotifications)
        {
            if (!includeFriendsNotifications)
            {
                excludedTypes.Add(FRIENDSHIP_RECEIVED_TYPE);
                excludedTypes.Add(FRIENDSHIP_ACCEPTED_TYPE);
            }
        }

        public override void WriteJson(JsonWriter writer, List<INotification> value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<INotification> ReadJson(JsonReader reader, Type objectType, List<INotification> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var notificationsList = JObject.Load(reader).Value<JArray>("notifications");
            existingValue ??= new List<INotification>();

            foreach (var item in notificationsList)
            {
                var notification = item as JObject;
                if (notification == null)
                    continue;

                var type = notification["type"]?.Value<string>();
                if (type == null)
                    continue;

                if (excludedTypes.Contains(type))
                    continue;

                INotification notificationObject = type switch
                {
                    "events_started" => new EventStartedNotification(),
                    "events_ended" => new EventEndedNotification(),
                    "reward_assignment" => new RewardAssignedNotification(),
                    "reward_in_progress" => new RewardInProgressNotification(),
                    "badge_granted" => new BadgeGrantedNotification(),
                    "credits_goal_completed" => new MarketplaceCreditsNotification(),
                    FRIENDSHIP_RECEIVED_TYPE => new FriendRequestReceivedNotification(),
                    FRIENDSHIP_ACCEPTED_TYPE => new FriendRequestAcceptedNotification(),
                    _ => null
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
