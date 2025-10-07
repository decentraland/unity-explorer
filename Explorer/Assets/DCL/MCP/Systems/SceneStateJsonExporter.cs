using System;
using System.Collections.Generic;
using System.Text;
using CRDT.Deserializer;
using CRDT.Memory;
using CRDT.Protocol;
using CrdtEcsBridge.PoolsProviders;
using CrdtEcsBridge.Components;
using DCL.ECS7;
using CrdtEcsBridge.Serialization;
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

        // Простая мапа известных componentId -> имя. При желании можно дернуть из глобального контейнера, но для логов хватит плоской карты.
        private static bool TryGetComponentName(int id, out string name)
        {
            switch (id)
            {
                case 1:
                    name = nameof(ComponentID.TRANSFORM);
                    return true;
                case 1017:
                    name = nameof(ComponentID.MATERIAL);
                    return true;
                case 1018:
                    name = nameof(ComponentID.MESH_COLLIDER);
                    return true;
                case 1019:
                    name = nameof(ComponentID.MESH_RENDERER);
                    return true;
                case 1041:
                    name = nameof(ComponentID.GLTF_CONTAINER);
                    return true;
                case 1048:
                    name = nameof(ComponentID.ENGINE_INFO);
                    return true;
                case 1049:
                    name = nameof(ComponentID.REALM_INFO);
                    return true;
                case 1054:
                    name = nameof(ComponentID.MAIN_CAMERA);
                    return true;
                case 1060:
                    name = nameof(ComponentID.VISIBILITY_COMPONENT);
                    return true;
                case 1062:
                    name = nameof(ComponentID.TEXT_SHAPE);
                    return true;
                case 1072:
                    name = nameof(ComponentID.UI_TRANSFORM);
                    return true;
                case 1074:
                    name = nameof(ComponentID.UI_BACKGROUND);
                    return true;
                case 1075:
                    name = nameof(ComponentID.UI_TEXT);
                    return true;
                case 1087:
                    name = nameof(ComponentID.AVATAR_SHAPE);
                    return true;
                case 1089:
                    name = nameof(ComponentID.AVATAR_EQUIPPED_DATA);
                    return true;
                case 1091:
                    name = nameof(ComponentID.AVATAR_BASE);
                    return true;
                case 1106:
                    name = nameof(ComponentID.PLAYER_IDENTITY_DATA);
                    return true;
                case 1209:
                    name = nameof(ComponentID.POINTER_LOCK);
                    return true;

                // Дополнить по мере необходимости
                default:
                    name = null;
                    return false;
            }
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
            if (componentId == ComponentID.TRANSFORM)
                return TryWriteTransform(sb, data);

            if (SDKComponentsRegistryLocator.TryGet(out ISDKComponentsRegistry registry) && registry.TryGet(componentId, out SDKComponentBridge bridge))
            {
                try
                {
                    object instance = bridge.Pool.Rent();

                    try
                    {
                        bridge.Serializer.DeserializeInto(instance, data);

                        if (instance is IMessage message)
                        {
                            string json = JsonFormatter.Default.Format(message);
                            sb.Append(',');
                            sb.Append("\"decoded\":");
                            sb.Append(json);
                            return true;
                        }
                    }
                    finally { bridge.Pool.Release(instance); }
                }
                catch { }
            }

            return false;
        }

        private static bool TryWriteTransform(StringBuilder sb, ReadOnlySpan<byte> data)
        {
            try
            {
                // pos(Vector3) rot(Quaternion) scale(Vector3) parent(int32)
                ReadOnlySpan<byte> p = data;
                var px = BitConverter.ToSingle(p.Slice(0, 4));
                var py = BitConverter.ToSingle(p.Slice(4, 4));
                var pz = BitConverter.ToSingle(p.Slice(8, 4));
                var rx = BitConverter.ToSingle(p.Slice(12, 4));
                var ry = BitConverter.ToSingle(p.Slice(16, 4));
                var rz = BitConverter.ToSingle(p.Slice(20, 4));
                var rw = BitConverter.ToSingle(p.Slice(24, 4));
                var sx = BitConverter.ToSingle(p.Slice(28, 4));
                var sy = BitConverter.ToSingle(p.Slice(32, 4));
                var sz = BitConverter.ToSingle(p.Slice(36, 4));
                var parentId = BitConverter.ToInt32(p.Slice(40, 4));

                sb.Append(',');
                sb.Append("\"decoded\":{");
                sb.Append("\"position\":{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"x\":{0},\"y\":{1},\"z\":{2}", px, py, pz);
                sb.Append("},\"rotation\":{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"x\":{0},\"y\":{1},\"z\":{2},\"w\":{3}", rx, ry, rz, rw);
                sb.Append("},\"scale\":{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\"x\":{0},\"y\":{1},\"z\":{2}", sx, sy, sz);
                sb.Append("},\"parentId\":");
                sb.Append(parentId);
                sb.Append('}');
                return true;
            }
            catch { return false; }
        }
    }
}
