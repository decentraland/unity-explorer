using DCL.Quality;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class ForceAllLightsOn
    {
        public static void Apply()
        {
            var asset = AssetDatabase.LoadAssetAtPath<QualitySettingsAsset>("Assets/DCL/Quality/Quality Settings.asset");

            if (asset == null)
            {
                Debug.LogError("[ForceAllLightsOn] Quality Settings.asset not found");
                EditorApplication.Exit(1);
                return;
            }

            foreach (QualitySettingsAsset.QualityCustomLevel level in asset.customSettings)
            {
                level.dynamicLights.SceneLimitations.LightsPerParcel = 50;
                level.dynamicLights.SceneLimitations.HardMaxLightCount = 999;

                if (level.dynamicLights.SpotLightsLods.Count > 0)
                {
                    var last = level.dynamicLights.SpotLightsLods[^1];
                    last.Distance = 500;
                    level.dynamicLights.SpotLightsLods[^1] = last;
                }

                if (level.dynamicLights.PointLightsLods.Count > 0)
                {
                    var last = level.dynamicLights.PointLightsLods[^1];
                    last.Distance = 500;
                    level.dynamicLights.PointLightsLods[^1] = last;
                }
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log("[ForceAllLightsOn] Applied to all quality presets");
        }
    }
}
