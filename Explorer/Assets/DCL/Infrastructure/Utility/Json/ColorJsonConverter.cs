using Newtonsoft.Json;
using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Utility.Json
{
    [Preserve]
    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(nameof(value.r));
            writer.WriteValue(value.r);
            writer.WritePropertyName(nameof(value.g));
            writer.WriteValue(value.g);
            writer.WritePropertyName(nameof(value.b));
            writer.WriteValue(value.b);
            writer.WritePropertyName(nameof(value.a));
            writer.WriteValue(value.a);

            writer.WriteEndObject();
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return default(Color);

            reader.Read();

            while (reader.TokenType == JsonToken.PropertyName)
            {
                if (reader.Value is string name)
                {
                    switch (name)
                    {
                        case nameof(Color.r):
                            existingValue.r = (float?)reader.ReadAsDouble() ?? 0f;
                            break;
                        case nameof(Color.g):
                            existingValue.g = (float?)reader.ReadAsDouble() ?? 0f;
                            break;
                        case nameof(Color.b):
                            existingValue.b = (float?)reader.ReadAsDouble() ?? 0f;
                            break;
                        case nameof(Color.a):
                            existingValue.a = (float?)reader.ReadAsDouble() ?? 0f;
                            break;
                    }
                }
                else { reader.Skip(); }

                reader.Read();
            }

            return existingValue;
        }
    }
}
