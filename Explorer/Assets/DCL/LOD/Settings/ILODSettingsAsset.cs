using System.Collections.Generic;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using UnityEngine;

namespace DCL.LOD
{
    public interface ILODSettingsAsset
    {
        //Threshold for bucket partition (inclusive)
        int[] LodPartitionBucketThresholds { get;  }
        int SDK7LodThreshold { get; set;  }


        //Texture array settings. Default resolutions and their default sizes
        TextureArrayResolutionDescriptor[] DefaultTextureArrayResolutionDescriptors { get;  }
        int ArraySizeForMissingResolutions { get; }
        int CapacityForMissingResolutions { get; }

        //Debug features
        bool IsColorDebugging { get; set; }
        Color[] LODDebugColors { get;  }
        DebugCube DebugCube { get; }

        bool EnableLODStreaming { get; set; }
        float AsyncIntegrationTimeMS { get;  }

    }
}
