using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Notification.Serialization
{
    public class NotificationJsonDtoConverter : JsonConverter<List<INotification>>
    {
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

                INotification notificationObject = type switch
                {
                    "events_started" => new EventStartedNotification(),
                    "events_ended" => new EventEndedNotification(),
                    "reward_assignment" => new RewardAssignedNotification(),
                    "governance_announcement" => new IncomingRewardNotification(),
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
