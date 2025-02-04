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

        public bool DirectOnly;
        public bool InderctOnly;
        public bool FullRoadsLayout;
        [Space] public bool Run;

        private int currentPrefabId;

        public void Update()
        {
            if (!Run) return;

            if (!FullRoadsLayout && currentPrefabId != Mathf.Min(PrefabId, originalPrefabs.Length - 1))
                AddPrefabToService();

            if (InderctOnly)
                instancingService.RenderIndirect();
            else if (DirectOnly)
                instancingService.RenderDirect();
            else
                instancingService.Render();
        }

        private void OnDisable()
        {
            instancingService.Clear();
        }

        [ContextMenu(nameof(AddPrefabToService))]
        public void AddPrefabToService()
        {
            instancingService.Clear();

            currentPrefabId = Mathf.Min(PrefabId, originalPrefabs.Length - 1);
            instancingService.AddToIndirect(originalPrefabs[currentPrefabId].indirectCandidates);
            instancingService.AddToDirect(originalPrefabs[currentPrefabId].directCandidates);
        }

        [ContextMenu(nameof(AddRoadsToService))]
        public void AddRoadsToService()
        {
            instancingService.Clear();

            instancingService.AddToIndirect(RoadsConfig.IndirectCandidates);
            instancingService.AddToDirect(RoadsConfig.DirectCandidates);
        }
    }
}
