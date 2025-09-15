using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DCL.PlacesAPIService.Serialization
{
    [Preserve]
    public class PlacesByCategoryJsonDtoConverter : JsonConverter<List<OptimizedPlaceInMapResponse>>
    {
        public override void WriteJson(JsonWriter writer, List<OptimizedPlaceInMapResponse> value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            foreach (var item in value)
                serializer.Serialize(writer, item);

            writer.WriteEndArray();
        }

        public override List<OptimizedPlaceInMapResponse> ReadJson(JsonReader reader, Type objectType, List<OptimizedPlaceInMapResponse> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            existingValue ??= new List<OptimizedPlaceInMapResponse>();

            var rootObject = JObject.Load(reader).ToObject<RootObject>();
            foreach ((string? key, DataObject? value) in rootObject.data)
            {
                existingValue.Add(new OptimizedPlaceInMapResponse()
                {
                    base_position = ConvertStringToVector2Int(key),
                    name = value.title
                });
            }
            return existingValue;
        }

        private static Vector2Int ConvertStringToVector2Int(string input)
        {
            string[] parts = input.Trim().Split(',');
            return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
        }

        private class RootObject
        {
            public Dictionary<string, DataObject> data { get; set; }
        }

        private class DataObject
        {
            public string title { get; set; }
        }
    }
}
