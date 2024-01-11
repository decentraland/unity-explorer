using System;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace ECS.SceneLifeCycle.Components
{
    public struct SceneLOD
    {
        private GameObject currentLOD;
        private readonly Transform lodContainer;
        private readonly SceneDefinitionComponent sceneDefinitionComponent;

        private readonly GameObject[] lods;
        public int currentLODLevel;
        private readonly Vector2Int[] lodBucketLimits;
        private readonly IComponentPool<Transform> transformPool;

        public SceneLOD(SceneDefinitionComponent sceneDefinitionComponent, Vector2Int[] lodBucketsLimits,
            IComponentPool<Transform> transformPool)
        {
            this.sceneDefinitionComponent = sceneDefinitionComponent;
            this.transformPool = transformPool;
            lodBucketLimits = lodBucketsLimits;
            lodContainer = transformPool.Get();
            lodContainer.name =
                $"({sceneDefinitionComponent.Parcels[0].x},{sceneDefinitionComponent.Parcels[0].y}) LODS";
            lodContainer.position = ParcelMathHelper.GetPositionByParcelPosition(sceneDefinitionComponent.Parcels[0]);
            currentLOD = null;
            currentLODLevel = -1;
            lods = new GameObject[2];
        }

        //DEBUG METHOD. WE SHOULD LOAD AS ABs
        public void LoadLod()
        {
            var parcel = $"{sceneDefinitionComponent.Parcels[0].x},{sceneDefinitionComponent.Parcels[0].y}";

            var lod2Prefab = Resources.Load<GameObject>($"{parcel}/25/{sceneDefinitionComponent.Definition.id}_lod2");
            if (lod2Prefab != null)
            {
                var lod3Prefab =
                    Resources.Load<GameObject>($"{parcel}/5/{sceneDefinitionComponent.Definition.id}_lod2");
                lods[0] = CreateLOD(lod2Prefab, "_lod2");
                lods[1] = CreateLOD(lod3Prefab, "_lod3");
            }
        }

        private GameObject CreateLOD(GameObject lod2Prefab, string name)
        {
            var newLOD = Object.Instantiate(lod2Prefab, lodContainer);
            newLOD.name = name;
            newLOD.gameObject.SetActive(false);
            return newLOD;
        }

        //DEBUG METHOD. WE SHOULD WORK WITH ABs AND THEIR CACHES. INVESTIGATE DEFERENCE AND WHEN IT SHOULD OCCUR
        public void Dispose()
        {
            if (lodContainer == null) return; //This was an empty lod all around
            foreach (var lod in lods)
                Object.Destroy(lod);
            transformPool.Release(lodContainer);
        }

        public void HideLod()
        {
            if (lods[0] == null) return; //Lods not loaded, nothing to do

            lods[0].SetActive(false);
            lods[1].SetActive(false);
        }

        public void UpdateLOD(byte partitionBucket)
        {
            if (lods[0] == null) return; //Lods not loaded, nothing to do

            currentLOD?.gameObject.SetActive(false);
            SetCurrentLODLevel(partitionBucket);
            currentLOD = lods[currentLODLevel - 2];
            currentLOD.SetActive(true);
        }

        private void SetCurrentLODLevel(byte partitionBucket)
        {
            if (partitionBucket > lodBucketLimits[0][0] && partitionBucket <= lodBucketLimits[0][1])
                currentLODLevel = 2;
            else if (partitionBucket > lodBucketLimits[1][0])
                currentLODLevel = 3;
        }
    }
    
}