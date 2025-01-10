using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace DCL.Roads.Playground
{
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        private readonly HashSet<int> processedRoads = new ();
        private readonly Dictionary<LODInstanceData, List<Matrix4x4>> propMatrices = new ();
        private readonly List<(LODInstanceData, Matrix4x4[])> propFastMatrices = new ();

        public PrefabInstancingData[] originalPrefabs;
        public RoadSettingsAsset roadsConfig;

        public bool DebugZero;
        public int DebugMeshId;

        [Header("DEBUG")]
        public Mesh[] Props;
        public int InstancesAmount;

        public void Awake()
        {
            foreach (RoadDescription roadDescription in roadsConfig.RoadDescriptions)
            {
                if (!processedRoads.Add(roadDescription.GetHashCode())) continue;

                PrefabInstancingData prefab = originalPrefabs.FirstOrDefault(op => op.name == roadDescription.RoadModel);
                if (prefab == null) continue;

                foreach (LODInstanceData mesh in prefab.InstancesData)
                {
                    if (propMatrices.TryGetValue(mesh, out List<Matrix4x4> matrix))
                        matrix.AddRange(AdjustedMatrices(roadDescription, mesh));
                    else
                        propMatrices.Add(mesh, AdjustedMatrices(roadDescription, mesh));
                }
            }

            List<Mesh> props = new List<Mesh>();
            foreach (KeyValuePair<LODInstanceData, List<Matrix4x4>> propPair in propMatrices)
            {
                propFastMatrices.Add((propPair.Key, propPair.Value.ToArray()));
                props.Add(propPair.Key.MeshLOD[0]);
            }
            Props = props.ToArray();
        }

        public void Update()
        {
            if (!DebugZero)
            {
                foreach ((LODInstanceData mesh, Matrix4x4[] matrices) prop in propFastMatrices)
                    Graphics.RenderMeshInstanced(new RenderParams(prop.mesh.Material), prop.mesh.MeshLOD[0], 0, prop.matrices);
            }
            else
            {
                if (DebugMeshId < 0)
                {
                    foreach (LODInstanceData data in originalPrefabs[0].InstancesData)
                        Graphics.RenderMeshInstanced(new RenderParams(data.Material), data.MeshLOD[0], 0, data.Matrices.ToArray());
                }
                else
                {
                    LODInstanceData data = originalPrefabs[0].InstancesData[DebugMeshId];
                    Graphics.RenderMeshInstanced(new RenderParams(data.Material), data.MeshLOD[0], 0, data.Matrices.ToArray());
                }
            }
        }

        private static List<Matrix4x4> AdjustedMatrices(RoadDescription roadDescription, LODInstanceData mesh)
        {
            var rootTransform = Matrix4x4.TRS(roadDescription.RoadCoordinate.ParcelToPositionFlat(), roadDescription.Rotation.SelfOrIdentity(), Vector3.one);

            var adjustedMatrices = new List<Matrix4x4>();
            foreach (Matrix4x4 instanceMatrix in mesh.Matrices)
                adjustedMatrices.Add(rootTransform * instanceMatrix);

            return adjustedMatrices;
        }
    }
}
