using DCL.Roads.GPUInstancing;
using DCL.Roads.GPUInstancing.Playground;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.Roads.Playground
{
    public class RoadsGPUInstancingPlayground2 : MonoBehaviour
    {
        public PrefabInstanceDataBehaviour[] originalPrefab;

        public bool Debug;

        public void Start()
        {

        }

        public void Update()
        {
            if (Debug)
            {
                DrawMeshesInstanced(originalPrefab[0].PrefabInstance.Meshes);

                foreach (LODGroupData lodGroup in originalPrefab[0].PrefabInstance.LODGroups)
                    DrawMeshesInstanced(lodGroup.LODs[0].Meshes);
            }
        }

        private void DrawMeshesInstanced(MeshData[] meshes)
        {
            foreach (MeshData mesh in meshes)
            {
                var matrices = new List<Matrix4x4>();
                var rootTransform = Matrix4x4.TRS(mesh.Transform.position, mesh.Transform.rotation.SelfOrIdentity(), Vector3.one);
                matrices.Add(rootTransform);

                for (var i = 0; i < mesh.Materials.Length; i++)
                {
                    var renderParams = new RenderParams(mesh.Materials[i])
                    {
                        shadowCastingMode = mesh.ShadowCastingMode,
                        receiveShadows = mesh.ReceiveShadows,
                    };

                    Graphics.RenderMeshInstanced(renderParams, mesh.Mesh, i, matrices);
                }
            }
        }
    }
}
