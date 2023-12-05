using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Ipfs
{
    /// <summary>
    ///     Provides support for polymorphic definition of "position"
    /// </summary>
    public class SpawnPointConverter : JsonConverter<IpfsTypes.SceneMetadata.SpawnPoint>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, IpfsTypes.SceneMetadata.SpawnPoint value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override IpfsTypes.SceneMetadata.SpawnPoint ReadJson(JsonReader reader, Type objectType, IpfsTypes.SceneMetadata.SpawnPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);
            var spawnPoint = new IpfsTypes.SceneMetadata.SpawnPoint();

            spawnPoint.name = jsonObject["name"].Value<string>();
            JToken position = jsonObject["position"];

            // Check if 'position' is an array or a single object
            if (position["x"].Type == JTokenType.Array)
            {
                // Deserialize as MultiPosition
                spawnPoint.MP = position.ToObject<IpfsTypes.SceneMetadata.SpawnPoint.MultiPosition>();
            }
            else
            {
                // Deserialize as SinglePosition
                spawnPoint.SP = position.ToObject<IpfsTypes.SceneMetadata.SpawnPoint.SinglePosition>();
            }

            return spawnPoint;
        }
    }
}
