using System;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.LOD.Systems
{
    public static class LODGroupPoolUtils
    {
        public static void ReleaseLODGroup(LODGroup lodGroup)
        {
            GenerateDefaultLODs(lodGroup);
        }

        public static void GenerateDefaultLODs(LODGroup lodGroup)
        {
            lodGroup.name = "LODGroup";
            var lod0 = new UnityEngine.LOD();
            var lod1 = new UnityEngine.LOD();
            lod0.renderers = Array.Empty<Renderer>();
            lod1.renderers = Array.Empty<Renderer>();
            lod0.screenRelativeTransitionHeight = 1;
            lod1.screenRelativeTransitionHeight = 0.9999f;
            lodGroup.SetLODs(new []
            {
                lod0, lod1
            });
        }

        public static void PrewarmLODGroupPool(GameObjectPool<LODGroup> lodGroupPool, int preWarmValue)
        {
            var lodGroupArray = new LODGroup[preWarmValue];
            for (int i = 0; i < preWarmValue; i++)
                lodGroupArray[i] = lodGroupPool.Get();
            for (int i = 0; i < preWarmValue; i++)
                lodGroupPool.Release(lodGroupArray[i]);
        }

        public static LODGroup CreateLODGroup()
        {
            var lodGroup = new GameObject().AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            GenerateDefaultLODs(lodGroup);
            return lodGroup;
        }
    }
}