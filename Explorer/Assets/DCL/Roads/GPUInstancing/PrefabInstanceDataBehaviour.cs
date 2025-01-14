using System;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class PrefabInstanceData // : IEquatable<LODInstanceData>
    {
        public MeshData[] Meshes;
        public LODGroupData[] LODGroups;
    }

    public class PrefabInstanceDataBehaviour: MonoBehaviour
    {
        public PrefabInstanceData PrefabInstance;

        [ContextMenu(nameof(CollectSelfData))]
        private void CollectSelfData()
        {
            GetLODGroupsData();
            GetMeshesData();
        }

        private void GetLODGroupsData()
        {
            var LODGroupsBehaviours = GetComponentsInChildren<LODGroupDataBehaviour>();
            PrefabInstance.LODGroups = new LODGroupData[LODGroupsBehaviours.Length];
            for (var i = 0; i < LODGroupsBehaviours.Length; i++)
                PrefabInstance.LODGroups[i] = LODGroupsBehaviours[i].LODGroupData;
        }
        private void GetMeshesData()
        {
            var meshDataBehaviours = GetComponentsInChildren<MeshDataBehaviour>();
            PrefabInstance.Meshes = new MeshData[meshDataBehaviours.Length];
            for (var i = 0; i < meshDataBehaviours.Length; i++)
                PrefabInstance.Meshes[i] = meshDataBehaviours[i].meshData;
        }
    }
}
