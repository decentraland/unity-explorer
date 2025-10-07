using System;
using System.Collections.Generic;
using System.Text;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.Components;
using Google.Protobuf;

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

                    if (TryGetComponentName(m.ComponentId, out string name))
                    {
                        sb.Append(',');
                        sb.Append("\"componentName\":\"");
                        sb.Append(name);
                        sb.Append("\"");
                    }
                    sb.Append(',');
                    sb.Append("\"timestamp\":");
                    sb.Append(m.Timestamp);

                    // раскодировать payload, если знаем тип
                    if (TryWriteDecodedComponent(sb, m.ComponentId, m.Data.Memory.Span))
                    {
                        // decodedData уже записан
                    }
                    else
                    {
                        sb.Append(',');
                        sb.Append("\"data\":\"");
                        AppendBase64(sb, m.Data.Memory.Span);
                        sb.Append("\"");
                    }
                    break;
                case CRDTMessageType.DELETE_COMPONENT:
                    sb.Append(',');
                    sb.Append("\"componentId\":");
                    sb.Append(m.ComponentId);

                    if (TryGetComponentName(m.ComponentId, out name))
                    {
                        sb.Append(',');
                        sb.Append("\"componentName\":\"");
                        sb.Append(name);
                        sb.Append("\"");
                    }
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

        private static bool TryGetComponentName(int id, out string name)
        {
            if (SDKComponentsRegistryLocator.TryGet(out ISDKComponentsRegistry registry) && registry.TryGet(id, out SDKComponentBridge bridge))
            {
                name = bridge.ComponentType.Name;
                return true;
            }

            name = null;
            return false;
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

        private static bool TryWriteDecodedComponent(StringBuilder sb, int componentId, ReadOnlySpan<byte> data)
        {
            // Единый путь: через реестр и сериализатор
            if (SDKComponentsRegistryLocator.TryGet(out ISDKComponentsRegistry registry) && registry.TryGet(componentId, out SDKComponentBridge bridge))
            {
                object instance = null;
                try
                {
                    instance = bridge.Pool.Rent();
                    bridge.Serializer.DeserializeInto(instance, data);

                    if (instance is IMessage message)
                    {
                        string json = JsonFormatter.Default.Format(message);
                        sb.Append(',');
                        sb.Append("\"decoded\":");
                        sb.Append(json);
                        return true;
                    }

                    // Непротобуфовые типы (например, SDKTransform)
                    if (TryWriteDecodedNonProtobuf(sb, instance))
                        return true;
                }
                catch { }
                finally
                {
                    if (instance != null)
                        bridge.Pool.Release(instance);
                }
            }

            return false;
        }

        private static bool TryWriteDecodedNonProtobuf(StringBuilder sb, object instance)
        {
            if (instance is CrdtEcsBridge.Components.Transform.SDKTransform t)
            {
                sb.Append(',');
                sb.Append("\"decoded\":{");
                sb.Append("\"position\":{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"x\":{0},\"y\":{1},\"z\":{2}", t.Position.Value.x, t.Position.Value.y, t.Position.Value.z);
                sb.Append("},\"rotation\":{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"x\":{0},\"y\":{1},\"z\":{2},\"w\":{3}", t.Rotation.Value.x, t.Rotation.Value.y, t.Rotation.Value.z, t.Rotation.Value.w);
                sb.Append("},\"scale\":{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"x\":{0},\"y\":{1},\"z\":{2}", t.Scale.x, t.Scale.y, t.Scale.z);
                sb.Append("},\"parentId\":");
                sb.Append(t.ParentId.Id);
                sb.Append('}');
                return true;
            }

            return false;
        }
    }
}
