using UnityEngine;
using DCL.Landscape.Config;
using GPUInstancerPro;
using GPUInstancerPro.TerrainModule;

[CreateAssetMenu(fileName = "GPUIAsset", menuName = "DCL/Landscape/GPUI Asset")]
public class GPUIAssets : ScriptableObject
{
    public GPUITreeManager treeManagerPrefab;
    public GPUIDetailManager detailsManagerPrefab;
    public GPUIDebuggerCanvas debuggerCanvasPrefab;
    public LandscapeAsset[] treesToOverride;
    public LandscapeAsset[] detailToOverride;
}
