using DCL.AssetsProvision;
using DCL.Character;
using Diagnostics.ReportsHandling;
using ECS.Prioritization;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class StaticSettings : IDCLPluginSettings
    {
        [field: Header(nameof(StaticSettings))] [field: Space]
        [field: SerializeField]
        public CharacterObjectRef CharacterObject { get; private set; }

        [field: SerializeField]
        public float StartYPosition { get; private set; } = 1.0f;

        [field: SerializeField]
        public ReportHandlingSettingsRef ReportHandlingSettings { get; private set; }

        [field: SerializeField]
        public PartitionSettingsRef PartitionSettings { get; private set; }

        [field: SerializeField]
        public RealmPartitionSettingsRef RealmPartitionSettings { get; private set; }

        [Serializable]
        public class CharacterObjectRef : ComponentReference<CharacterObject>
        {
            public CharacterObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class PartitionSettingsRef : AssetReferenceT<PartitionSettingsAsset>
        {
            public PartitionSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class ReportHandlingSettingsRef : AssetReferenceT<ReportsHandlingSettings>
        {
            public ReportHandlingSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class RealmPartitionSettingsRef : AssetReferenceT<RealmPartitionSettingsAsset>
        {
            public RealmPartitionSettingsRef(string guid) : base(guid) { }
        }
    }
}
