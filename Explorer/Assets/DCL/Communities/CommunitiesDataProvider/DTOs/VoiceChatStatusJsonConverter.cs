using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Preserve]
    public class VoiceChatStatusJsonConverter : JsonConverter<GetCommunityResponse.VoiceChatStatus>
    {
        public override void WriteJson(JsonWriter writer, GetCommunityResponse.VoiceChatStatus value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("isActive");
            writer.WriteValue(value.isActive);
            writer.WritePropertyName("participantCount");
            writer.WriteValue(value.participantCount);
            writer.WritePropertyName("moderatorCount");
            writer.WriteValue(value.moderatorCount);
            writer.WriteEndObject();
        }

        public override GetCommunityResponse.VoiceChatStatus ReadJson(JsonReader reader, Type objectType, GetCommunityResponse.VoiceChatStatus existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return new GetCommunityResponse.VoiceChatStatus
                {
                    isActive = false,
                    participantCount = 0,
                    moderatorCount = 0
                };
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var jsonObject = JObject.Load(reader);

                return new GetCommunityResponse.VoiceChatStatus
                {
                    isActive = jsonObject["isActive"]?.Value<bool>() ?? false,
                    participantCount = jsonObject["participantCount"]?.Value<int>() ?? 0,
                    moderatorCount = jsonObject["moderatorCount"]?.Value<int>() ?? 0
                };
            }

            return serializer.Deserialize<GetCommunityResponse.VoiceChatStatus>(reader);
        }
    }
}
