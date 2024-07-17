using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.Utilities.Json
{
    public class OriginalJsonContainerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            typeof(IPreserveOriginalJson).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            IPreserveOriginalJson result = (IPreserveOriginalJson)Activator.CreateInstance(objectType);
            result.OriginalJson = jObject.ToString(Formatting.None);

            serializer.Populate(jObject.CreateReader(), result);

            return result;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var jObject = JObject.FromObject(value, serializer);

            jObject.Remove("OriginalJson");

            jObject.WriteTo(writer);
        }
    }
}
