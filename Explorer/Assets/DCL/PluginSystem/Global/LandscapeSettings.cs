using DCL.Landscape.Settings;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class LandscapeSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LandscapeSettings))] [field: Space]
        [field: SerializeField]
        internal LandscapeDataRef landscapeData { get; private set; }

        [Serializable]
        public class LandscapeDataRef : AssetReferenceT<LandscapeData>
        {
            public LandscapeDataRef(string guid) : base(guid) { }
        }
    }
}
