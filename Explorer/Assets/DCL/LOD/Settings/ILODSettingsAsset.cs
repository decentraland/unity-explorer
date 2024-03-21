using UnityEngine;

namespace DCL.LOD
{
    public interface ILODSettingsAsset
    {
        public bool IsColorDebuging { get; set; }

        //TODO: Clean this up, remove it from settings and create LODDebugSettings
        public int[] LodPartitionBucketThresholds { get; set; }

        public Color[] LODDebugColors { get; set; }

        public GameObject FaillingCube { get; set; }
    }
}
