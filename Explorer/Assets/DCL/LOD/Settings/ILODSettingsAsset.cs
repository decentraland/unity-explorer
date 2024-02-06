using UnityEngine;

namespace DCL.LOD
{
    public interface ILODSettingsAsset
    {
        public bool IsColorDebuging { get; set; }

        public int[] LodPartitionBucketThresholds { get; set; }

        public Color[] LODDebugColors { get; set; }
    }
}
