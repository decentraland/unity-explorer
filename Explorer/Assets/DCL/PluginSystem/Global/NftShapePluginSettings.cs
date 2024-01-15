using DCL.PluginSystem;
using DCL.SDKComponents.NftShape.Frames;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Frame
{
    [Serializable]
    public class NftShapePluginSettings : IDCLPluginSettings
    {
        [field: Header("Nft Shape")] [field: Space]
        [field: SerializeField]
        public NftShapeSettings Settings { get; private set; }
    }
}
