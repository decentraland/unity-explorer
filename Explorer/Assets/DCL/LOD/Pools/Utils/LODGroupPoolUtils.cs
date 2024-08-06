using System;
using System.Buffers;
using DCL.Optimization.Pools;
using UnityEngine;

namespace DCL.LOD.Systems
{
    public static class LODGroupPoolUtils
    {
        public static int DEFAULT_LOD_AMOUT = 2;
        public static readonly ArrayPool<Renderer> RENDERER_ARRAY_POOL = ArrayPool<Renderer>.Create();
        
        public static void ReleaseLODGroup(LODGroup lodGroup)
        {
            foreach (var loD in lodGroup.GetLODs())
                RENDERER_ARRAY_POOL.Return(loD.renderers, true);
            ResetToDefaultLOD(lodGroup);
        }

        public static void ResetToDefaultLOD(LODGroup lodGroup)
        {
            lodGroup.name = "LODGroup";
            var lods = new UnityEngine.LOD[DEFAULT_LOD_AMOUT];
            for (int i = 0; i < DEFAULT_LOD_AMOUT; i++)
                lods[i] = new UnityEngine.LOD(1f - i * 0.0001f, Array.Empty<Renderer>());
            lodGroup.SetLODs(lods);
        }

        public static void PrewarmLODGroupPool(IComponentPool<LODGroup> lodGroupPool, int preWarmValue)
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
            ResetToDefaultLOD(lodGroup);
            return lodGroup;
        }
    }
}