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

            writer.WritePropertyName("status");
            writer.WriteValue(value.Status);

            writer.WritePropertyName("startTime");
            writer.WriteValue(value.StartTime);

            writer.WritePropertyName("endTime");
            writer.WriteValue(value.EndTime);

            if (value.HeadersInfo != null)
            {
                writer.WritePropertyName("headersInfo");
                serializer.Serialize(writer, value.HeadersInfo);
            }

            writer.WriteEndObject();
        }

        public override WebRequestDump.Envelope? ReadJson(JsonReader reader, Type objectType, WebRequestDump.Envelope? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            CommonArguments? commonArguments = null;
            Type? requestType = null;
            Type? argsType = null;
            object? args = null;
            WebRequestHeadersInfo? headersInfo = null;
            DateTime startTime = DateTime.MinValue;
            DateTime endTime = DateTime.MinValue;
            WebRequestDump.Envelope.StatusKind statusKind = WebRequestDump.Envelope.StatusKind.NOT_CONCLUDED;

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
                            requestType = Type.GetType(requestTypeString);

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

                    case "startTime":
                        reader.Read();
                        startTime = serializer.Deserialize<DateTime>(reader);
                        break;

                    case "endTime":
                        reader.Read();
                        endTime = serializer.Deserialize<DateTime>(reader);
                        break;

                    case "status":
                        reader.Read();
                        statusKind = serializer.Deserialize<WebRequestDump.Envelope.StatusKind>(reader);
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }

            if (!commonArguments.HasValue || argsType == null)
                throw new JsonSerializationException("Required properties 'commonArguments' or 'argsType' are missing");

            var envelope = new WebRequestDump.Envelope(requestType, commonArguments.Value, argsType, args, headersInfo, startTime);
            envelope.Conclude(statusKind, endTime);

            return envelope;
        }
    }
}
