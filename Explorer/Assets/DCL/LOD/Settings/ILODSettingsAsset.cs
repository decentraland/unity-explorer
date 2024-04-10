using System.Collections.Generic;
using DCL.AssetsProvision;
using UnityEngine;

namespace DCL.LOD
{
    public interface ILODSettingsAsset
    {
        //Threshold for bucket partition (inclusive) 
        public int[] LodPartitionBucketThresholds { get; set; }

        //Texture array settings
        public int TextureArrayMinSize { get; }
        int[] DefaultTextureArrayResolutions { get;  }

        //Debug features        
        public bool IsColorDebuging { get; set; }
        public Color[] LODDebugColors { get; set; }
        public FaillingLODCube FaillingCube { get; set; }

        public bool EnableLODStreaming { get; set; }

        
        
    }
}
