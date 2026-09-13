// Data ScriptableObject holding the engine-wide runtime settings authored in
// GPUIRuntimeSettings.asset. GPUIRuntimeSettingsLoader hands it to
// GPUICoreAPI.ApplyRuntimeSettings at boot; instancingBoundsSize seeds
// TreeRendererService's fallback world-bounds volume. Field names and types
// mirror the asset YAML exactly so every authored value binds to a real
// member. The script GUID is pinned in the .meta to
// 951c13b4dce928945a2e8bd7f5fd7928 so the .asset reference resolves.

using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIRuntimeSettings : ScriptableObject
    {
        // YAML keys — must match Assets/DCL/Landscape/Assets/GPUI/GPUIRuntimeSettings.asset.
        // Types are inferred from the YAML literals (ints for the *Type/*Mode/*Condition
        // enum-like fields, Vector3 for the bounds size, float for the shadow distance,
        // a managed list of UnityEngine.Object for billboardAssets, ints for the two
        // addressable toggles).

        [SerializeField] public int cameraLoadingType;
        [SerializeField] public int occlusionCullingCondition;
        [SerializeField] public int occlusionCullingMode;
        [SerializeField] public Vector3 instancingBoundsSize = new Vector3(1000f, 1000f, 1000f);
        [SerializeField] public float defaultHDRPShadowDistance = 250f;
        [SerializeField] public List<Object> billboardAssets = new List<Object>();
        [SerializeField] public int loadShadersFromAddressables = 1;
        [SerializeField] public int loadResourcesFromAddressables = 1;
    }
}
