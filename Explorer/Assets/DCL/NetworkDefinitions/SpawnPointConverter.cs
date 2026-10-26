using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine.Scripting;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Provides support for polymorphic definition of "position"
    /// </summary>
    [Preserve]
    public class SpawnPointCoordinateConverter : JsonConverter<SceneMetadata.SpawnPoint.Coordinate>
    {
        public override void WriteJson(JsonWriter writer, SceneMetadata.SpawnPoint.Coordinate value, JsonSerializer serializer)
        {
            if (value.SingleValue is { } v)
            {
                writer.WriteValue(v);
                return;
            }

            writer.WriteStartArray();
            foreach (float item in value.MultiValue ?? Array.Empty<float>()) writer.WriteValue(item);
            writer.WriteEndArray();
        }

        public override SceneMetadata.SpawnPoint.Coordinate ReadJson(JsonReader reader, Type objectType, SceneMetadata.SpawnPoint.Coordinate existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            // Scene metadata is creator-authored: an explicit null, a null inside the range or a value that is not a number
            // is treated the same as an absent coordinate instead of failing the whole deserialization.
            try
            {
                return token.Type switch
                       {
                           JTokenType.Null => default,
                           JTokenType.Array => new SceneMetadata.SpawnPoint.Coordinate { MultiValue = token.ToObject<float[]>() },
                           _ => new SceneMetadata.SpawnPoint.Coordinate { SingleValue = token.ToObject<float>() },
                       };
            }
            catch (Exception e) when (e is JsonException or FormatException or ArgumentException)
            {
                return default;
            }
        }
    }
}
