using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Diagnostics;
using System.Collections.Concurrent;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        // ===================== TriggerArea: запросы и обработка =====================
        public struct MCPCreateTriggerAreaRequest
        {
            public string RequestId;

            // Transform
            public float X, Y, Z;
            public float SX, SY, SZ;
            public float Yaw, Pitch, Roll;
            public int ParentId;

            // Trigger Area
            public string MeshType; // "Box" | "Sphere"
        }

        private static readonly ConcurrentQueue<MCPCreateTriggerAreaRequest> triggerAreaRequests = new ();

        public static void EnqueueTriggerArea(in MCPCreateTriggerAreaRequest request) =>
            triggerAreaRequests.Enqueue(request);

        public void ProcessTriggerAreaRequests(World world, IComponentPool<PBTriggerArea> triggerAreaPool, IComponentPool<PBMeshRenderer> meshRendererPool)
        {
            while (triggerAreaRequests.TryDequeue(out MCPCreateTriggerAreaRequest req))
            {
                try
                {
                    var position = new Vector3(req.X, req.Y, req.Z);
                    var scale = new Vector3(req.SX, req.SY, req.SZ);
                    var rotation = Quaternion.Euler(req.Pitch, req.Yaw, req.Roll);

                    // 1) Создаём триггер-область
                    Begin(world, position, scale, rotation, req.ParentId)
                       .AddTriggerArea(world, triggerAreaPool, req);

                    // 2) One-time reaction: создаём новую сущность с цилиндром
                    var cylReq = new MCPCreateMeshRendererRequest
                    {
                        RequestId = req.RequestId + "_reaction_cylinder",
                        X = req.X,
                        Y = req.Y,
                        Z = req.Z,
                        SX = req.SX,
                        SY = req.SY,
                        SZ = req.SZ,
                        Yaw = req.Yaw,
                        Pitch = req.Pitch,
                        Roll = req.Roll,
                        ParentId = req.ParentId,
                        MeshType = "Cylinder",
                        RadiusTop = 0.5f,
                        RadiusBottom = 0.5f,
                    };

                    Begin(world, position, scale, rotation, req.ParentId)
                       .AddMeshRenderer(world, meshRendererPool, cylReq);

                    ReportHub.Log(ReportCategory.DEBUG, $"[MCP TriggerArea] Created TriggerArea + one-time Cylinder for request {req.RequestId}");
                }
                catch (System.Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP TriggerArea] Failed to process request {req.RequestId}: {e.Message}"); }
            }
        }

        public MCPSceneEntitiesBuilder AddTriggerArea(World world,
            IComponentPool<PBTriggerArea> pool,
            MCPCreateTriggerAreaRequest req)
        {
            PBTriggerArea pb = pool.Get();

            // Map mesh type
            TriggerAreaMeshType meshType = req.MeshType == "Sphere"
                ? TriggerAreaMeshType.TamtSphere
                : TriggerAreaMeshType.TamtBox;

            pb.Mesh = meshType;
            pb.IsDirty = true;
            world.Add(entity, pb);

            ecsToCRDTWriter.PutMessage<PBTriggerArea, TriggerAreaMeshType>(static (component, mt) => { component.Mesh = mt; }, currentCRDTEntity, meshType);

            return this;
        }
    }
}
