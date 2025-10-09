using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using System.Collections.Concurrent;
using UnityEngine;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        // ===================== Transform: изменение позиции/поворота/масштаба существующей entity =====================
        public struct MCPSetEntityTransformRequest
        {
            public string RequestId;
            public int EntityId; // CRDT entity id

            // Optional position
            public float? X, Y, Z;

            // Optional scale
            public float? SX, SY, SZ;

            // Optional rotation (Euler degrees)
            public float? Yaw, Pitch, Roll;
        }

        private static readonly ConcurrentQueue<MCPSetEntityTransformRequest> setTransformRequests = new ();

        public static void EnqueueSetEntityTransform(in MCPSetEntityTransformRequest request) =>
            setTransformRequests.Enqueue(request);

        public void ProcessSetTransformRequests(World world)
        {
            while (setTransformRequests.TryDequeue(out MCPSetEntityTransformRequest req))
            {
                try
                {
                    var crdt = new CRDTEntity(req.EntityId);

                    if (!entitiesMap.ContainsKey(crdt))
                    {
                        ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Transform] Entity {req.EntityId} not found; skipping transform set for request {req.RequestId}");
                        continue;
                    }

                    // Получаем текущие значения из мира, чтобы применять частичные обновления
                    Entity e = entitiesMap[crdt];

                    if (!world.Has<SDKTransform>(e))
                    {
                        ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Transform] Entity {req.EntityId} has no SDKTransform; skipping");
                        continue;
                    }

                    ref SDKTransform sdk = ref world.Get<SDKTransform>(e);

                    var newPos = new Vector3(
                        req.X ?? sdk.Position.Value.x,
                        req.Y ?? sdk.Position.Value.y,
                        req.Z ?? sdk.Position.Value.z
                    );

                    var newScale = new Vector3(
                        req.SX ?? sdk.Scale.x,
                        req.SY ?? sdk.Scale.y,
                        req.SZ ?? sdk.Scale.z
                    );

                    var newRot = Quaternion.Euler(
                        req.Pitch ?? sdk.Rotation.Value.eulerAngles.x,
                        req.Yaw ?? sdk.Rotation.Value.eulerAngles.y,
                        req.Roll ?? sdk.Rotation.Value.eulerAngles.z
                    );

                    // 1) Обновляем мир (визуально)
                    sdk.Position.Value = newPos;
                    sdk.Scale = newScale;
                    sdk.Rotation.Value = newRot;

                    // 2) Отправляем CRDT сообщение (с теми же значениями)
                    ecsToCRDTWriter.PutMessage<SDKTransform, (Vector3 Pos, Vector3 Scale, Quaternion Rot)>(static (sdkTransform, data) =>
                    {
                        sdkTransform.Position.Value = data.Pos;
                        sdkTransform.Scale = data.Scale;
                        sdkTransform.Rotation.Value = data.Rot;
                    }, crdt, (newPos, newScale, newRot));

                    ReportHub.Log(ReportCategory.DEBUG, $"[MCP Transform] Updated entity {req.EntityId} transform (pos/rot/scale)");
                }
                catch (System.Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Transform] Failed to process transform request {req.RequestId}: {e.Message}"); }
            }
        }
    }
}
