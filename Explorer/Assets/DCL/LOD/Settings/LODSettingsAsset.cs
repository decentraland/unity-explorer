using System;
using System.Collections.Generic;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using UnityEngine;

namespace DCL.LOD
{
    [CreateAssetMenu(menuName = "Create LOD Settings", fileName = "LODSettings", order = 0)]
    public class LODSettingsAsset : ScriptableObject, ILODSettingsAsset
    {
        [field: SerializeField] public int[] LodPartitionBucketThresholds { get; set; } = { 1, 2, 5 };

        [field: SerializeField] public int DefaultArraySize { get; set; } = 100;

        //TODO (Juani): No textures higher than should arrive. Check in AB converter
        [field: SerializeField] public TextureArrayResolutionDescriptor[] DefaultTextureArrayResolutionDescriptors { get; set; } =
        {
            new (256, 500), new (512, 100), new (1024, 100)
        };

        [field: SerializeField] public bool IsColorDebuging { get; set; }
        [field: SerializeField] public Color[] LODDebugColors { get; set; } = { Color.green, Color.yellow, Color.red };
        [field: SerializeField] public FaillingLODCube FaillingCube { get; set; }
        [field: SerializeField] public bool EnableLODStreaming { get; set; }
    }


}
