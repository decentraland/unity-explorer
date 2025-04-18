﻿using System;
using System.Collections.Generic;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using UnityEngine;

namespace DCL.LOD
{
    [CreateAssetMenu(fileName = "LODSettings", menuName = "DCL/Various/LOD Settings")]
    public class LODSettingsAsset : ScriptableObject, ILODSettingsAsset
    {
        [field: SerializeField] public int[] LodPartitionBucketThresholds { get; set; } =
        {
            5,
        };
        [field: SerializeField] public int SDK7LodThreshold { get; set; } = 2;

        [field: SerializeField] public int UnloadTolerance { get; set; } = 1;

        [field: SerializeField] public TextureArrayResolutionDescriptor[] DefaultTextureArrayResolutionDescriptors { get; set; } =
        {
            new (512, 100, 5), new (1024, 50, 1),
        };

        [field: SerializeField] public int ArraySizeForMissingResolutions { get; set; } = 10;
        [field: SerializeField] public int CapacityForMissingResolutions { get; set; } = 1;

        public bool IsColorDebugging { get; set; }
        [field: SerializeField] public Color[] LODDebugColors { get; set; } = { Color.green, Color.yellow, Color.red };
        [field: SerializeField] public DebugCube DebugCube { get; set; }
        [field: SerializeField] public bool EnableLODStreaming { get; set; }

        //TODO (Juani): Commented until re-implemented
        //public float AsyncIntegrationTimeMS { get; set; } = 33;
    }
}
