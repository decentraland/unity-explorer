using System;
using System.Collections.Generic;
using System.Text;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.PoolsProviders;

namespace DCL.MCP.Systems
{
    internal static class SceneStateJsonExporter
    {
        private static CRDTDeserializer deserializer;

        // Минимальный JSON без внешних зависимостей: строим вручную
        public static string ExportStateToJson(PoolableByteArray data)
        {
            EnsureDeserializer();
            ReadOnlyMemory<byte> mem = data.Memory;
            var messages = new List<CRDTMessage>(256);
            deserializer.DeserializeBatch(ref mem, messages);

            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append("\"schema\":\"dcl-crdt-json@0\",");
            sb.Append("\"messages\":[");

            for (var i = 0; i < messages.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendMessageJson(sb, messages[i]);
            }

            sb.Append("]}");

            // освободить payload'ы
            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Data.Memory.Length > 0)
                    messages[i].Data.Dispose();
            }

            return sb.ToString();
        }

        private static void EnsureDeserializer()
        {
            if (deserializer != null) return;
            deserializer = new CRDTDeserializer(CRDTPooledMemoryAllocator.Create());
        }

        private static void AppendMessageJson(StringBuilder sb, in CRDTMessage m)
        {
            sb.Append('{');
            sb.Append("\"type\":\"");
            sb.Append(m.Type.ToString());
            sb.Append("\",");
            sb.Append("\"entity\":{");
            sb.Append("\"id\":");
            sb.Append(m.EntityId.Id);
            sb.Append(',');
            sb.Append("\"number\":");
            sb.Append(m.EntityId.EntityNumber);
            sb.Append(',');
            sb.Append("\"version\":");
            sb.Append(m.EntityId.EntityVersion);
            sb.Append('}');

            switch (m.Type)
            {
                case CRDTMessageType.PUT_COMPONENT:
                case CRDTMessageType.APPEND_COMPONENT:
                    sb.Append(',');
                    sb.Append("\"componentId\":");
                    sb.Append(m.ComponentId);
                    sb.Append(',');
                    sb.Append("\"timestamp\":");
                    sb.Append(m.Timestamp);
                    sb.Append(',');
                    sb.Append("\"data\":\"");
                    AppendBase64(sb, m.Data.Memory.Span);
                    sb.Append("\"");
                    break;
                case CRDTMessageType.DELETE_COMPONENT:
                    sb.Append(',');
                    sb.Append("\"componentId\":");
                    sb.Append(m.ComponentId);
                    sb.Append(',');
                    sb.Append("\"timestamp\":");
                    sb.Append(m.Timestamp);
                    break;
                case CRDTMessageType.DELETE_ENTITY:
                    // только entityId
                    break;
            }

            sb.Append('}');
        }

        private static void AppendBase64(StringBuilder sb, ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0) return;

            // Convert.ToBase64String не имеет Span-оверлоада в старых профилях, скопируем во временный массив
            byte[] tmp = bytes.ToArray();
            string b64 = Convert.ToBase64String(tmp);

            // escape для JSON — base64 не требует дополнительных экранирований
            sb.Append(b64);
        }
    }
}
