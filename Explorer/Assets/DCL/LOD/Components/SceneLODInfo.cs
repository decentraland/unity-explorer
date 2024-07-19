using System.Collections.Generic;
using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public List<byte> LoadedLODs;
        public byte CurrentLODLevel;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public bool IsDirty;

        private LODGroup lodGroup;
        private int currentSuccessfullLOD;

        private UnityEngine.LOD lod0;
        private UnityEngine.LOD lod1;
        
        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);

            Object.Destroy(lodGroup.gameObject);
        }

        public static SceneLODInfo Create()
        {
            var lodGroup = new GameObject().AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;

            var lod0 = new UnityEngine.LOD(1.0f, null);
            var lod1 = new UnityEngine.LOD(0.999f, null);
            lodGroup.SetLODs(new[] { lod0, lod1 });

            return new SceneLODInfo
            {
                LoadedLODs = new List<byte>(),
                CurrentLODLevel = byte.MaxValue,
                lodGroup = lodGroup,
                lod0 = lod0,
                lod1 = lod1
            };
        }

        public void RecalculateLODGroup(LODAsset newLod)
        {
            LoadedLODs.Add(newLod.LodKey.Level);
            if (newLod.State == LODAsset.LOD_STATE.SUCCESS)
            {
                newLod.Root.transform.SetParent(lodGroup.transform);
                currentSuccessfullLOD++;

                if (newLod.LodKey.Level == 0)
                    lod0.renderers = newLod.Root.GetComponentsInChildren<Renderer>();
                else if (newLod.LodKey.Level == 1)
                    lod1.renderers = newLod.Root.GetComponentsInChildren<Renderer>();

                if (currentSuccessfullLOD == 1 && newLod.LodKey.Level == 0)
                {
                    lod0.screenRelativeTransitionHeight = 0.1f;
                    lod1.screenRelativeTransitionHeight = 0.099f;
                }
                else if (currentSuccessfullLOD == 1 && newLod.LodKey.Level == 1)
                {
                    lod0.screenRelativeTransitionHeight = 0.99f;
                    lod1.screenRelativeTransitionHeight = 0.1f;
                }
                else if (currentSuccessfullLOD == 2)
                {
                    lod0.screenRelativeTransitionHeight = 0.5f;
                    lod1.screenRelativeTransitionHeight = 0.1f;
                }

                UnityEngine.LOD[] lods = { lod0, lod1 };
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }
    }

}
