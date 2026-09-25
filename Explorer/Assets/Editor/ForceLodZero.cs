using DCL.LOD;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class ForceLodZero
    {
        public static void Apply()
        {
            var asset = AssetDatabase.LoadAssetAtPath<LODSettingsAsset>("Assets/DCL/LOD/Settings/LODSettings.asset");

            if (asset == null)
            {
                Debug.LogError("[ForceLodZero] LODSettings.asset not found");
                EditorApplication.Exit(1);
                return;
            }

            asset.LodPartitionBucketThresholds = new[] { 255 };
            asset.SDK7LodThreshold = 999;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ForceLodZero] LodPartitionBucketThresholds set to {asset.LodPartitionBucketThresholds[0]}, SDK7LodThreshold set to {asset.SDK7LodThreshold}");
        }
    }
}
