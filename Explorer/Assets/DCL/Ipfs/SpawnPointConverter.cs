using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Provides support for polymorphic definition of "position"
    /// </summary>
    public class SpawnPointCoordinateConverter : JsonConverter<SceneMetadata.SpawnPoint.Coordinate>
    {
        public override void WriteJson(JsonWriter writer, SceneMetadata.SpawnPoint.Coordinate value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override SceneMetadata.SpawnPoint.Coordinate ReadJson(JsonReader reader, Type objectType, SceneMetadata.SpawnPoint.Coordinate existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            return token.Type == JTokenType.Array
                ? new SceneMetadata.SpawnPoint.Coordinate { MultiValue = token.ToObject<float[]>() }
                : new SceneMetadata.SpawnPoint.Coordinate { SingleValue = token.ToObject<float>() };
        }
    }
}
