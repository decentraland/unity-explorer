using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingServicePlayground : MonoBehaviour
    {
        private readonly GPUInstancingService instancingService = new ();

        public RoadSettingsAsset RoadsConfig;

        public GPUInstancingPrefabData_Old[] originalPrefabs;
        [Min(0)] public int PrefabId;

        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;

        public bool OnePrefabDebug;
        [Space] public bool Run;

        private int currentPrefabId;

        public void Update()
        {
            if (!Run) return;

            if (OnePrefabDebug && currentPrefabId != Mathf.Min(PrefabId, originalPrefabs.Length - 1))
                AddPrefabToService();

            instancingService.RenderIndirect();
        }

        private void OnDisable()
        {
            instancingService.Dispose();
        }

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (GPUInstancingPrefabData_Old prefab in originalPrefabs)
            {
                prefab.CollectSelfData();
                prefab.ShowVisuals();
            }
        }

        [ContextMenu(nameof(RoadConfigCollect))]
        private void RoadConfigCollect()
        {
            RoadsConfig.CollectGPUInstancingCandidates(ParcelsMin, ParcelsMax);
        }

        private void AddPrefabToService()
        {
            instancingService.Dispose();

            currentPrefabId = Mathf.Min(PrefabId, originalPrefabs.Length - 1);
            instancingService.AddToIndirect(originalPrefabs[currentPrefabId].indirectCandidates);
            instancingService.AddToDirect(originalPrefabs[currentPrefabId].directCandidates);
        }

        [ContextMenu(nameof(AddRoadsToService))]
        private void AddRoadsToService()
        {
            instancingService.Dispose();

            instancingService.AddToIndirect(RoadsConfig.IndirectCandidates);
            instancingService.AddToDirect(RoadsConfig.DirectCandidates);
        }
    }
}
