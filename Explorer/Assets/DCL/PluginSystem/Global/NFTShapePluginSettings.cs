using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.WebRequests.WebContentSizes.Sizes;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class NFTShapePluginSettings : IDCLPluginSettings
    {
        [field: Header("Nft Shape")] [field: Space]
        [field: SerializeField]
        public NFTShapeSettings Settings { get; private set; }

        [field: SerializeField]
        public MaxSize MaxSizeOfNftForDownload { get; private set; }
    }
}
