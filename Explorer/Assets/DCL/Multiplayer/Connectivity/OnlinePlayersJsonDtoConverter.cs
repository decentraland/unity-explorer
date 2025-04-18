using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Connectivity
{
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

            var rootObject = JObject.Load(reader).ToObject<RootObject>();
            foreach (DataObject rootObjectPeer in rootObject.peers)
            {
                existingValue.Add(new OnlineUserData()
                {
                    position = ToVector3(rootObjectPeer.position[0], rootObjectPeer.position[2]),
                    avatarId = rootObjectPeer.address
                });
            }
            return existingValue;
        }

        private static Vector3 ToVector3(float x, float z) =>
            new (Convert.ToInt32(x), 0, Convert.ToInt32(z));

        private class RootObject
        {
            public List<DataObject> peers { get; set; }
        }

        private class DataObject
        {
            public string address { get; set; }
            public float[] position { get; set; }
        }
    }
}
