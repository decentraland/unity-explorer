using UnityEngine;

namespace DCL.Roads.Playground
{
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        public PrefabInstancingData prefab;
        private RenderParams _rp;

        private LODInstanceData instanceData;

        public int id;

        public void Start()
        {
            // GameObject.Instantiate(prefab);
        }

        public void Update()
        {
            var instanceData = prefab.InstancesData[id];

            // Graphics.DrawMeshInstanced(instanceData.MeshLOD[0], 0, instanceData.Material, instanceData.Matrices.ToArray());

            _rp = new RenderParams(instanceData.Material);
            Graphics.RenderMeshInstanced(_rp, instanceData.MeshLOD[0], 0, instanceData.Matrices.ToArray());
        }
    }
}
