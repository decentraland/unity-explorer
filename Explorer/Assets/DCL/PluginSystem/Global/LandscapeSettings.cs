using DCL.Landscape.Settings;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class LandscapeSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LandscapeSettings))] [field: Space] [field: SerializeField]
        public StaticSettings.RealmPartitionSettingsRef realmPartitionSettings;

        [field: SerializeField] public LandscapeDataRef landscapeData;
        [field: SerializeField] public ParcelsRef emptyParcels;
        [field: SerializeField] public ParcelsRef ownedParcels;

        [Serializable]
        public class LandscapeDataRef : AssetReferenceT<LandscapeData>
        {
            public LandscapeDataRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class ParcelsRef : AssetReferenceT<TextAsset>
        {
            public ParcelsRef(string guid) : base(guid) { }
        }
    }
}
