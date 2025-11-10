using Newtonsoft.Json;
using System;
using UnityEngine.Scripting;

namespace DCL.WebRequests.Dumper
{
    [Preserve]
    public class EnvelopeJsonConverter : JsonConverter<WebRequestDump.Envelope>
    {
        public override void WriteJson(JsonWriter writer, WebRequestDump.Envelope? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName("requestType");
            writer.WriteValue(value.RequestType.AssemblyQualifiedName);

            writer.WritePropertyName("commonArguments");
            serializer.Serialize(writer, value.CommonArguments);

            writer.WritePropertyName("argsType");
            writer.WriteValue(value.ArgsType.AssemblyQualifiedName);

            writer.WritePropertyName("args");
            serializer.Serialize(writer, value.Args);

            writer.WritePropertyName("headersInfo");
            serializer.Serialize(writer, value.HeadersInfo);

            writer.WriteEndObject();
        }

        public override WebRequestDump.Envelope? ReadJson(JsonReader reader, Type objectType, WebRequestDump.Envelope? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            reader.Read(); // Start object

            CommonArguments? commonArguments = null;
            Type? requestType = null;
            Type? argsType = null;
            object? args = null;
            WebRequestHeadersInfo headersInfo = default;

            // First pass: read argsType to know how to deserialize args
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                string? propertyName = reader.Value?.ToString();

                switch (propertyName)
                {
                    case "requestType":
                        reader.Read();
                        string? requestTypeString = reader.Value?.ToString();

                        if (!string.IsNullOrEmpty(requestTypeString))
                            argsType = Type.GetType(requestTypeString);

                        break;

                    case "commonArguments":
                        reader.Read();
                        commonArguments = serializer.Deserialize<CommonArguments>(reader);
                        break;

                    case "argsType":
                        reader.Read();
                        string? argsTypeString = reader.Value?.ToString();

                        if (!string.IsNullOrEmpty(argsTypeString))
                            argsType = Type.GetType(argsTypeString);

                        break;

                    case "args":
                        reader.Read();

                        // Deserialize args using the resolved type from argsType
                        if (argsType != null)
                            args = serializer.Deserialize(reader, argsType);
                        else
                            args = serializer.Deserialize<object>(reader);

                        break;

                    case "headersInfo":
                        reader.Read();
                        headersInfo = serializer.Deserialize<WebRequestHeadersInfo>(reader);
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }

            if (!commonArguments.HasValue || argsType == null)
                throw new JsonSerializationException("Required properties 'commonArguments' or 'argsType' are missing");

            return new WebRequestDump.Envelope(requestType, commonArguments.Value, argsType, args, headersInfo);
        }
    }
}
