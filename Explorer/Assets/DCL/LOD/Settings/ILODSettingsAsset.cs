using System.Collections.Generic;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using UnityEngine;

namespace DCL.LOD
{
    public interface ILODSettingsAsset
    {
        //Threshold for bucket partition (inclusive) 
        public int[] LodPartitionBucketThresholds { get; set; }

        //Texture array settings. Default resolutions and their default sizes
        TextureArrayResolutionDescriptor[] DefaultTextureArrayResolutionDescriptors { get;  }
        public int DefaultArraySize { get; set; }

        
        //Debug features        
        public bool IsColorDebuging { get; set; }
        public Color[] LODDebugColors { get; set; }
        public FaillingLODCube FaillingCube { get; set; }

        public bool EnableLODStreaming { get; set; }

        
        
    }
}
