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
        // ===================== GLTFContainer: запросы и обработка =====================
        public struct MCPCreateGltfContainerRequest
        {
            public string RequestId;

            // Transform
            public float X, Y, Z;
            public float SX, SY, SZ;
            public float Yaw, Pitch, Roll;
            public int ParentId;

            // GLTF
            public string Src;
        }

        private static readonly ConcurrentQueue<MCPCreateGltfContainerRequest> gltfContainerRequests = new ();

        public static void EnqueueGltfContainer(in MCPCreateGltfContainerRequest request) =>
            gltfContainerRequests.Enqueue(request);

        public void ProcessGltfContainerRequests(World world, IComponentPool<PBGltfContainer> pool)
        {
            while (gltfContainerRequests.TryDequeue(out MCPCreateGltfContainerRequest req))
            {
                try
                {
                    var position = new Vector3(req.X, req.Y, req.Z);
                    var scale = new Vector3(req.SX, req.SY, req.SZ);
                    var rotation = Quaternion.Euler(req.Pitch, req.Yaw, req.Roll);

                    Begin(world, position, scale, rotation, req.ParentId)
                       .AddGltfContainer(world, pool, req);

                    ReportHub.Log(ReportCategory.DEBUG, $"[MCP GLTF] Created GltfContainer from request {req.RequestId} src={req.Src}");
                }
                catch (System.Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP GLTF] Failed to process request {req.RequestId}: {e.Message}"); }
            }
        }

        public MCPSceneEntitiesBuilder AddGltfContainer(World world,
            IComponentPool<PBGltfContainer> pool,
            MCPCreateGltfContainerRequest req)
        {
            PBGltfContainer pb = pool.Get();
            pb.Src = req.Src ?? string.Empty;
            pb.IsDirty = true;
            world.Add(entity, pb);

            ecsToCRDTWriter.PutMessage<PBGltfContainer, string>(static (component, src) => { component.Src = src ?? string.Empty; }, currentCRDTEntity, req.Src ?? string.Empty);

            return this;
        }
    }
}
