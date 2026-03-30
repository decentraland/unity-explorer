using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DCL.Utility
{
    public static class JsonUtils
    {
        public static T FromJsonWithNulls<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json);

        public static T SafeFromJson<T>(string json)
        {
            T returningValue = default(T);

            if (!string.IsNullOrEmpty(json))
            {
                try { returningValue = JsonUtility.FromJson<T>(json); }
                catch (ArgumentException e) { Debug.LogError(string.Format("ArgumentException Fail!... Json = {0} {1}", json, e)); }
            }

            return returningValue;
        }

        /// <summary>
        ///     Deserializes a color from a JSON token containing r, g, b, and a properties.
        /// </summary>
        /// <param name="jObject">The JSON token containing color data</param>
        /// <param name="color">The default color to return if the token is null</param>
        /// <returns>The deserialized color or the default if token is null</returns>
        public static Color DeserializeColor(JToken? jObject, Color color)
        {
            if (jObject == null) return color;

            color.r = jObject["r"]?.Value<float>() ?? 0;
            color.g = jObject["g"]?.Value<float>() ?? 0;
            color.b = jObject["b"]?.Value<float>() ?? 0;
            color.a = jObject["a"]?.Value<float>() ?? 1;

            return color;
        }

        /// <summary>
        ///     Serializes a Unity Color to JSON format with r, g, b, and a properties.
        /// </summary>
        /// <param name="writer">The JSON writer to write to</param>
        /// <param name="color">The color to serialize</param>
        public static void SerializeColor(JsonWriter writer, Color color)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(color.r);
            writer.WritePropertyName("g");
            writer.WriteValue(color.g);
            writer.WritePropertyName("b");
            writer.WriteValue(color.b);
            writer.WritePropertyName("a");
            writer.WriteValue(color.a);
            writer.WriteEndObject();
        }
    }
}
