using UnityEngine;

namespace DCL.Roads.Playground
{
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        public PrefabInstancingData prefab;

        public int id;

        private LODInstanceData instanceData;
        private RenderParams rp;

        public void Update()
        {
            if (id >= 0)
                DrawInstanced(id);
            else
                DrawAll();
        }

        private void DrawAll()
        {
            for (var i = 0; i < prefab.InstancesData.Length; i++)
                DrawInstanced(i);
        }

        private void DrawInstanced(int id)
        {
            LODInstanceData instanceData = prefab.InstancesData[id];

            rp = new RenderParams(instanceData.Material);
            Graphics.RenderMeshInstanced(rp, instanceData.MeshLOD[0], 0, instanceData.Matrices.ToArray());
        }
    }
}
