using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace DCL.Roads.Playground
{
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        public PrefabInstancingData originalPrefab;
        public RoadDescription roadDescription;

        private LODInstanceData instanceData;

        public void Update()
        {
            DrawAll(originalPrefab.InstancesData);
        }

        private void DrawAll(LODInstanceData[] prefabMeshes)
        {
            foreach (var mesh in prefabMeshes)
                DrawInstanced(mesh);
        }

        private void DrawInstanced(LODInstanceData instanceData)
        {
            Matrix4x4 rootTransform = Matrix4x4.TRS(roadDescription.RoadCoordinate.ParcelToPositionFlat(), roadDescription.Rotation, Vector3.one);

            List<Matrix4x4> adjustedMatrices = new List<Matrix4x4>();
            foreach (Matrix4x4 m in instanceData.Matrices)
                adjustedMatrices.Add(rootTransform * m);

            Graphics.RenderMeshInstanced(new RenderParams(instanceData.Material), instanceData.MeshLOD[0], 0, adjustedMatrices.ToArray());
        }
    }
}
