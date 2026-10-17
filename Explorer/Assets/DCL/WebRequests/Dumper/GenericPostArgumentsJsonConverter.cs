using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace DCL.WebRequests.Dumper
{
    [Preserve]
    public class GenericPostArgumentsJsonConverter : JsonConverter<GenericPostArguments>
    {
        public enum Kind
        {
            Raw = 0,
            WwwForm = 1,
            MultiForm = 2,
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, GenericPostArguments value, JsonSerializer serializer)
        {
            if (value.MultipartFormSections != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("kind");
                writer.WriteValue((int)Kind.MultiForm);
                writer.WriteStartArray();

                foreach (IMultipartFormSection? section in value.MultipartFormSections)
                {
                    // Can be serialized with private fields awareness
                    serializer.Serialize(writer, section);
                }

                writer.WriteEndArray();

                writer.WriteEndObject();

                return;
            }

            if (value.WWWForm != null)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("kind");
                writer.WriteValue((int)Kind.WwwForm);

                // TODO support

                writer.WriteEndObject();
                return;
            }

            // Raw Data
            writer.WriteStartObject();

            writer.WritePropertyName("kind");
            writer.WriteValue((int)Kind.Raw);
            writer.WritePropertyName("postData");

            if (value.UploadHandler != null)
            {
                // Serialized as raw as an upload handler is a memory optimization, not a different data structure
                writer.WriteValue(value.UploadHandler.Value.ToString());
            }
            else { writer.WriteValue(value.PostData); }


            writer.WritePropertyName("contentType");
            writer.WriteValue(value.ContentType);

            writer.WriteEndObject();
        }

        public override GenericPostArguments ReadJson(JsonReader reader, Type objectType, GenericPostArguments existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            reader.Read(); // Read property name "kind"
            var kind = (Kind)reader.ReadAsInt32()!.Value;

            switch (kind)
            {
                case Kind.Raw:
                    reader.Read(); // Read property name "postData"
                    string postData = reader.ReadAsString() ?? string.Empty;
                    reader.Read(); // Read property name "contentType"
                    string contentType = reader.ReadAsString() ?? string.Empty;
                    reader.Read(); // End object
                    return GenericPostArguments.Create(postData, contentType);

                case Kind.WwwForm:
                    // TODO: implement WWWForm deserialization when WriteJson is completed
                    reader.Read(); // End object
                    throw new NotImplementedException("WWWForm deserialization is not yet supported");

                case Kind.MultiForm:
                    reader.Read(); // Start array
                    var sections = new List<IMultipartFormSection>();

                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        // TODO support FileSection
                        MultipartFormDataSection? section = serializer.Deserialize<MultipartFormDataSection>(reader);

                        if (section != null)
                            sections.Add(section);
                    }

                    reader.Read(); // End object
                    return GenericPostArguments.CreateMultipartForm(sections);

                default:
                    throw new JsonSerializationException($"Unknown Kind value: {kind}");
            }
        }
    }
}
