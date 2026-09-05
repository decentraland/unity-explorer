using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DCL.Multiplayer.Connectivity
{
    [Preserve]
    public class OnlinePlayersJsonDtoConverter : JsonConverter<List<OnlineUserData>>
    {
        public override void WriteJson(JsonWriter writer, List<OnlineUserData>? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<OnlineUserData> ReadJson(JsonReader reader, Type objectType, List<OnlineUserData>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            existingValue ??= new List<OnlineUserData>();

            var root = JObject.Load(reader);
            var peers = root["peers"] as JArray;

            if (peers == null)
                return existingValue;

            foreach (JToken peer in peers)
            {
                string? address = peer["address"]?.Value<string>();
                var posArray = peer["position"] as JArray;

                if (address == null || posArray == null || posArray.Count < 3)
                    continue;

                JToken xToken = posArray[0];
                JToken zToken = posArray[2];

                if (xToken.Type == JTokenType.Null || zToken.Type == JTokenType.Null)
                    continue;

                existingValue.Add(new OnlineUserData
                {
                    position = ToVector3(xToken.Value<float>(), zToken.Value<float>()),
                    avatarId = address
                });
            }

            return existingValue;
        }

        private static Vector3 ToVector3(float x, float z) =>
            new (Convert.ToInt32(x), 0, Convert.ToInt32(z));
    }
}
