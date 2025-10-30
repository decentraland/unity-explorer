using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.Scripting;

namespace DCL.Multiplayer.Connectivity
{
    [Preserve]
    public class OnlinePlayerInWorldJsonDtoConverter : JsonConverter<OnlineUserData?>
    {
        // These prefixes were already fixed on backend side, but it might still popup for outdated catalysts.
        private readonly string[] sceneRoomPrefixes = {
            "world-prd-",
            "scene-room-"
        };

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
                worldName = RemovePrefixes(dataObject.world),
                avatarId = dataObject.wallet
            };

            return existingValue;

            string RemovePrefixes(string s)
            {
                foreach (string prefix in sceneRoomPrefixes)
                    s = s.Replace(prefix, string.Empty);
                
                return s;
            }
        }

        private class DataObject
        {
            public string wallet { get; set; }
            public string world { get; set; }
        }
    }
}
