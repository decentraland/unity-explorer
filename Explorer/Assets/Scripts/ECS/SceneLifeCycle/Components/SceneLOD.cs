using System;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace ECS.SceneLifeCycle.Components
{
    public struct SceneLOD : IDisposable
    {
        private GameObject currentLOD;
        private readonly Transform lodContainer;
        private readonly SceneDefinitionComponent sceneDefinitionComponent;

        private readonly GameObject[] lods;
        private int currentLODLevel;
        private readonly Vector2Int[] lodBucketLimits;

        public SceneLOD(SceneDefinitionComponent sceneDefinitionComponent, Vector2Int[] lodBucketLimits)
        {
            this.sceneDefinitionComponent = sceneDefinitionComponent;
            this.lodBucketLimits = lodBucketLimits;
            currentLODLevel = -1;
            lodContainer =
                new GameObject(
                        $"({sceneDefinitionComponent.Parcels[0].x},{sceneDefinitionComponent.Parcels[0].y}) LODS")
                    .transform;
            currentLOD = null;
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
                var positionByParcelPosition =
                    ParcelMathHelper.GetPositionByParcelPosition(sceneDefinitionComponent.Parcels[0]);
                lods[0] = Object.Instantiate(lod2Prefab, positionByParcelPosition, Quaternion.identity, lodContainer);
                lods[1] = Object.Instantiate(lod3Prefab, positionByParcelPosition, Quaternion.identity, lodContainer);
                lods[0].SetActive(false);
                lods[1].SetActive(false);
            }
        }

        //DEBUG METHOD. WE SHOULD WORK WITH ABs AND THEIR CACHES. INVESTIGATE DEFERENCE AND WHEN IT SHOULD OCCUR
        public void Dispose()
        {
            foreach (var lod in lods)
                Object.Destroy(lod);
            Object.Destroy(lodContainer.gameObject);
        }

        public void UpdateLOD(byte partitionBucket)
        {
            if (lods[0] == null) return; //Lods not loaded, nothing to do

            if (!ShouldSwapLOD(partitionBucket)) return;

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

        private bool ShouldSwapLOD(byte partitionBucket)
        {
            if (partitionBucket > lodBucketLimits[0][0] && partitionBucket <= lodBucketLimits[0][1] &&
                currentLODLevel == 2) return false;
            if (partitionBucket > lodBucketLimits[1][0] && currentLODLevel == 3) return false;
            return true;
        }
    }
}