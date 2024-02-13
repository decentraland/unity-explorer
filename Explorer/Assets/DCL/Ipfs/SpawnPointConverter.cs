using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Provides support for polymorphic definition of "position"
    /// </summary>
    public class SpawnPointConverter : JsonConverter<SceneMetadata.SpawnPoint>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, SceneMetadata.SpawnPoint value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override SceneMetadata.SpawnPoint ReadJson(JsonReader reader, Type objectType, SceneMetadata.SpawnPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var spawnPoint = new SceneMetadata.SpawnPoint();

            spawnPoint.name = jsonObject["name"].Value<string>();
            JToken position = jsonObject["position"];

            // Check if 'position' is an array or a single object
            if (position["x"].Type == JTokenType.Array)
            {
                // Deserialize as MultiPosition
                spawnPoint.MP = position.ToObject<SceneMetadata.SpawnPoint.MultiPosition>();
            }
            else
            {
                // Deserialize as SinglePosition
                spawnPoint.SP = position.ToObject<SceneMetadata.SpawnPoint.SinglePosition>();
            }

            return spawnPoint;
        }
    }
}
