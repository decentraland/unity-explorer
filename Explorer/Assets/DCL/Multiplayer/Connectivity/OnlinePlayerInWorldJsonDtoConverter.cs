using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Connectivity
{
    public class OnlinePlayerInWorldJsonDtoConverter : JsonConverter<OnlineUserData?>
    {
        private const string SCENE_ROOM_PREFIX = "world-prd-scene-room-";

        public override void WriteJson(JsonWriter writer, OnlineUserData? value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            serializer.Serialize(writer, value);
            writer.WriteEndArray();
        }

        public override OnlineUserData? ReadJson(JsonReader reader, Type objectType, OnlineUserData? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var dataObject = JObject.Load(reader).ToObject<DataObject>();

            //Returning null as if the user is not connected to a world the endpoint returns a different data structure
            if (dataObject == null)
                return null;

            existingValue = new OnlineUserData
            {
                worldName = dataObject.world.Replace(SCENE_ROOM_PREFIX, string.Empty),
                avatarId = dataObject.wallet
            };

            return existingValue;
        }

        private class DataObject
        {
            public string wallet { get; set; }
            public string world { get; set; }
        }
    }
}
