using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Font = DCL.ECSComponents.Font;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace DCL.MCP.Systems
{
    public partial class MCPSceneEntitiesBuilder
    {
        // ===================== MeshRenderer: запросы и обработка =====================
        public struct MCPCreateMeshRendererRequest
        {
            public string RequestId;

            // Transform
            public float X, Y, Z;
            public float SX, SY, SZ;
            public float Yaw, Pitch, Roll;
            public int ParentId;

            // Mesh type and params
            public string MeshType; // "Box" | "Sphere" | "Cylinder" | "Plane"
            public float? RadiusTop; // for Cylinder
            public float? RadiusBottom; // for Cylinder
            public List<float> Uvs; // for Box/Plane: 96-value map (optional)
        }

        private static readonly ConcurrentQueue<MCPCreateMeshRendererRequest> meshRendererRequests = new ();

        public static void EnqueueMeshRenderer(in MCPCreateMeshRendererRequest request) =>
            meshRendererRequests.Enqueue(request);

        // public void ProcessMeshRendererRequests(World world, IComponentPool<PBMeshRenderer> pool)
        // {
        //     while (meshRendererRequests.TryDequeue(out MCPCreateMeshRendererRequest req))
        //     {
        //         try
        //         {
        //             var position = new Vector3(req.X, req.Y, req.Z);
        //             var scale = new Vector3(req.SX, req.SY, req.SZ);
        //             var rotation = Quaternion.Euler(req.Pitch, req.Yaw, req.Roll);
        //
        //             Begin(position, scale, rotation, req.ParentId)
        //                .AddMeshRenderer(pool, req)
        //                .Build(world);
        //
        //             ReportHub.Log(ReportCategory.DEBUG, $"[MCP MeshRenderer] Created MeshRenderer from request {req.RequestId}");
        //         }
        //         catch (System.Exception e) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP MeshRenderer] Failed to process request {req.RequestId}: {e.Message}"); }
        //     }
        // }

        public MCPSceneEntitiesBuilder AddMeshRenderer(
            IComponentPool<PBMeshRenderer> pool,
            MCPCreateMeshRendererRequest req)
        {
            PBMeshRenderer mesh = pool.Get();

            // Select mesh type
            PBMeshRenderer.MeshOneofCase caseType = EnumTryParse(req.MeshType, PBMeshRenderer.MeshOneofCase.Box);

            switch (caseType)
            {
                case PBMeshRenderer.MeshOneofCase.Sphere:
                    mesh.Sphere = new PBMeshRenderer.Types.SphereMesh();
                    break;
                case PBMeshRenderer.MeshOneofCase.Cylinder:
                    mesh.Cylinder = new PBMeshRenderer.Types.CylinderMesh
                    {
                        RadiusTop = req.RadiusTop ?? 1f,
                        RadiusBottom = req.RadiusBottom ?? 1f,
                    };

                    break;
                case PBMeshRenderer.MeshOneofCase.Plane:
                    mesh.Plane = new PBMeshRenderer.Types.PlaneMesh();

                    if (req.Uvs != null && req.Uvs.Count > 0)
                        mesh.Plane.Uvs.AddRange(req.Uvs);

                    break;
                case PBMeshRenderer.MeshOneofCase.Box:
                default:
                    mesh.Box = new PBMeshRenderer.Types.BoxMesh();

                    if (req.Uvs != null && req.Uvs.Count > 0)
                        mesh.Box.Uvs.AddRange(req.Uvs);

                    break;
            }

            mesh.IsDirty = true;

            // Write CRDT message with selected type
            ecsToCRDTWriter.PutMessage<PBMeshRenderer, (PBMeshRenderer.MeshOneofCase CaseType, float RT, float RB, List<float> Uvs)>(static (pb, data) =>
            {
                switch (data.CaseType)
                {
                    case PBMeshRenderer.MeshOneofCase.Sphere:
                        pb.Sphere = new PBMeshRenderer.Types.SphereMesh();
                        break;
                    case PBMeshRenderer.MeshOneofCase.Cylinder:
                        pb.Cylinder = new PBMeshRenderer.Types.CylinderMesh { RadiusTop = data.RT, RadiusBottom = data.RB };
                        break;
                    case PBMeshRenderer.MeshOneofCase.Plane:
                        pb.Plane = new PBMeshRenderer.Types.PlaneMesh();

                        if (data.Uvs != null && data.Uvs.Count > 0)
                            pb.Plane.Uvs.AddRange(data.Uvs);

                        break;
                    case PBMeshRenderer.MeshOneofCase.Box:
                    default:
                        pb.Box = new PBMeshRenderer.Types.BoxMesh();

                        if (data.Uvs != null && data.Uvs.Count > 0)
                            pb.Box.Uvs.AddRange(data.Uvs);

                        break;
                }
            }, currentCRDTEntity, (caseType, req.RadiusTop ?? 1f, req.RadiusBottom ?? 1f, req.Uvs));

            // collectedComponents.Add(mesh);
            return this;
        }
    }
}
