using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public struct WearableAssetInstance
    {
        public GameObject GameObject;
        private PoolExtensions.Scope<List<MeshRenderer>> pooledMeshRendererList;

        public WearableAssetInstance(GameObject gameObject)
        {
            GameObject = gameObject;
            pooledMeshRendererList = GameObject.GetComponentsInChildrenIntoPooledList<MeshRenderer>(true);
        }

        public void UpdateMeshRendererPool()
        {
            pooledMeshRendererList.Dispose();
            pooledMeshRendererList = GameObject.GetComponentsInChildrenIntoPooledList<MeshRenderer>(true);
        }

        public void Release(IObjectPool<Material> materialPool)
        {
            for (var i = 0; i < pooledMeshRendererList.Value.Count; i++)
            {
                MeshRenderer meshRenderer = pooledMeshRendererList.Value[i];
                materialPool.Release(meshRenderer.material);
                meshRenderer.material = null;
            }

            pooledMeshRendererList.Dispose();
            UnityObjectUtils.SafeDestroy(GameObject);
        }
    }
}
