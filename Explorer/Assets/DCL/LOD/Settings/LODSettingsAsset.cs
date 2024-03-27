using System.Collections.Generic;
using DCL.AssetsProvision;
using UnityEngine;

namespace DCL.LOD
{
    [CreateAssetMenu(menuName = "Create LOD Settings", fileName = "LODSettings", order = 0)]
    public class LODSettingsAsset : ScriptableObject, ILODSettingsAsset
    {
        [field: SerializeField] public int[] LodPartitionBucketThresholds { get; set; } = { 1, 2, 5 };
        [field: SerializeField] public AssetReferenceMaterial DefaultLODMaterial { get; set; }
        [field: SerializeField] public int TextureArrayMinSize { get; } = 500;

        [field: SerializeField] public int[] DefaultTextureArrayResolutions { get; } =
        {
            256, 512
        };

        [field: SerializeField] public TextureFormat[] FormatsToCreate { get; } =
        {
            TextureFormat.BC7, TextureFormat.DXT1, TextureFormat.DXT5
        };
        [field: SerializeField] public bool IsColorDebuging { get; set; }
        [field: SerializeField] public Color[] LODDebugColors { get; set; } = { Color.green, Color.yellow, Color.red };
        [field: SerializeField] public GameObject FaillingCube { get; set; }
    }
}
