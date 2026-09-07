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
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<OnlineUserData>? ReadJson(JsonReader reader, Type objectType, List<OnlineUserData>? existingValue, bool hasExistingValue, JsonSerializer serializer)
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
                // A peers element may itself be a non-object (e.g. a literal null); JToken's indexer would
                // throw InvalidOperationException on those, which defeats the point of tolerating malformed payloads.
                if (peer is not JObject peerObject)
                    continue;

                string? address = peerObject["address"]?.Value<string>();
                var posArray = peerObject["position"] as JArray;

                if (address == null || posArray == null || posArray.Count < 3)
                    continue;

                // Value<float?>() yields null for null/missing/non-numeric tokens instead of throwing.
                float? x = posArray[0].Value<float?>();
                float? z = posArray[2].Value<float?>();

                if (x == null || z == null)
                    continue;

                existingValue.Add(new OnlineUserData
                {
                    position = ToVector3(x.Value, z.Value),
                    avatarId = address
                });
            }

            return existingValue;
        }

        private static Vector3 ToVector3(float x, float z) =>
            new (Convert.ToInt32(x), 0, Convert.ToInt32(z));
    }
}
