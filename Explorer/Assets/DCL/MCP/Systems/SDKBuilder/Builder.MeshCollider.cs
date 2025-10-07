using DCL.ECSComponents;
using DCL.Optimization.Pools;
using System.Collections.Concurrent;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        public struct MCPCreateMeshColliderRequest
        {
            public string RequestId;
            public string ColliderType; // "Box" | "Sphere" | "Cylinder" | "Plane"
            public uint CollisionMask; // bitmask of ColliderLayer
            public float? RadiusTop; // for Cylinder
            public float? RadiusBottom; // for Cylinder
        }

        private static readonly ConcurrentQueue<MCPCreateMeshColliderRequest> meshColliderRequests = new ();

        public static void EnqueueMeshCollider(in MCPCreateMeshColliderRequest request) =>
            meshColliderRequests.Enqueue(request);

        public void ProcessMeshColliderRequests(Arch.Core.World world, IComponentPool<PBMeshCollider> pool)
        {
            while (meshColliderRequests.TryDequeue(out MCPCreateMeshColliderRequest req))
            {
                try
                {
                    AddMeshCollider(pool, req);
                    Build(world);
                }
                catch (System.Exception e) { Diagnostics.ReportHub.LogError(Diagnostics.ReportCategory.DEBUG, $"[MCP MeshCollider] Failed to process request {req.RequestId}: {e.Message}"); }
            }
        }

        public MCPSceneEntitiesBuilder AddMeshCollider(IComponentPool<PBMeshCollider> pool, MCPCreateMeshColliderRequest req)
        {
            PBMeshCollider col = pool.Get();

            if (req.CollisionMask != 0)
                col.CollisionMask = req.CollisionMask;

            switch (req.ColliderType)
            {
                case "Sphere":
                    col.Sphere = new PBMeshCollider.Types.SphereMesh();
                    break;
                case "Cylinder":
                    col.Cylinder = new PBMeshCollider.Types.CylinderMesh
                    {
                        RadiusTop = req.RadiusTop ?? 0.5f,
                        RadiusBottom = req.RadiusBottom ?? 0.5f,
                    };

                    break;
                case "Plane":
                    col.Plane = new PBMeshCollider.Types.PlaneMesh();
                    break;
                case "Box":
                default:
                    col.Box = new PBMeshCollider.Types.BoxMesh();
                    break;
            }

            col.IsDirty = true;

            ecsToCRDTWriter.PutMessage<PBMeshCollider, MCPCreateMeshColliderRequest>(static (pb, data) =>
            {
                if (data.CollisionMask != 0)
                    pb.CollisionMask = data.CollisionMask;

                switch (data.ColliderType)
                {
                    case "Sphere":
                        pb.Sphere = new PBMeshCollider.Types.SphereMesh();
                        break;
                    case "Cylinder":
                        pb.Cylinder = new PBMeshCollider.Types.CylinderMesh
                        {
                            RadiusTop = data.RadiusTop ?? 0.5f,
                            RadiusBottom = data.RadiusBottom ?? 0.5f,
                        };

                        break;
                    case "Plane":
                        pb.Plane = new PBMeshCollider.Types.PlaneMesh();
                        break;
                    case "Box":
                    default:
                        pb.Box = new PBMeshCollider.Types.BoxMesh();
                        break;
                }
            }, currentCRDTEntity, req);

            collectedComponents.Add(col);
            return this;
        }
    }
}
