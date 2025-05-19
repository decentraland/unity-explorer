using System;
using DCL.Landscape.Settings;
#if GPUIPRO_PRESENT
using DCL.Landscape.Config;
using GPUInstancerPro;
using GPUInstancerPro.TerrainModule;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GPUIAsset", menuName = "DCL/Landscape/GPUI Asset")]
public class GPUIAssets : ScriptableObject
{
#if GPUIPRO_PRESENT
    public GPUITreeManager treeManagerPrefab;
    public GPUIDetailManager detailsManagerPrefab;
    public GPUIDebuggerCanvas debuggerCanvasPrefab;
    public LandscapeAsset[] treesToOverride;
    public LandscapeAsset[] detailToOverride;

#endif

    [Serializable]
    public class GPUIAssetsRef : AssetReferenceT<GPUIAssets>
    {
        public GPUIAssetsRef(string guid) : base(guid) { }
    }
}