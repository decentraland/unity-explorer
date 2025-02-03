using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using UnityEngine;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingServicePlayground : MonoBehaviour
    {
        private readonly GPUInstancingService instancingService = new ();

        public RoadSettingsAsset RoadsConfig;

        public GPUInstancingPrefabData[] originalPrefabs;
        [Min(0)] public int PrefabId;

        [Space] public bool Run;

        public void Update()
        {
            if (!Run) return;

            instancingService.RenderIndirect();
        }

        [ContextMenu(nameof(AddPrefabToService))]
        public void AddPrefabToService()
        {
            int prefabId = Mathf.Min(PrefabId, originalPrefabs.Length - 1);
            instancingService.Add(originalPrefabs[prefabId].indirectCandidates);
        }

        [ContextMenu(nameof(AddRoadsToService))]
        public void AddRoadsToService()
        {
            instancingService.Add(RoadsConfig.IndirectCandidates);
        }

        private void OnDisable()
        {
            instancingService.Clear();
        }
    }
}
