using System;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using UnityEngine;

namespace DCL.LOD
{
    /// <summary>
    /// Stub ILODSettingsAsset for WebGL where LOD streaming and road systems are disabled.
    /// Provides default values for VisualSceneStateResolver and other consumers; debug/capacity members are no-op.
    /// </summary>
    public class LODSettingsStub : ILODSettingsAsset
    {
        private static readonly int[] DefaultBucketThresholds = { 5 };
        private static readonly Color[] DefaultDebugColors = { Color.green, Color.yellow, Color.red };
        private static readonly TextureArrayResolutionDescriptor[] DefaultResolutions = Array.Empty<TextureArrayResolutionDescriptor>();

        public int[] LodPartitionBucketThresholds => DefaultBucketThresholds;
        public int SDK7LodThreshold { get; set; } = 2;
        public int UnloadTolerance { get; set; } = 1;
        public TextureArrayResolutionDescriptor[] DefaultTextureArrayResolutionDescriptors => DefaultResolutions;
        public int ArraySizeForMissingResolutions => 10;
        public int CapacityForMissingResolutions => 1;
        public bool IsColorDebugging { get; set; }
        public Color[] LODDebugColors => DefaultDebugColors;
        public DebugCube DebugCube => null!;
        public bool EnableLODStreaming { get; set; }
    }
}
