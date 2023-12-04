using DCL.AssetsProvision;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
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

        // Performance budgeting
        [field: Header("Performance Budgeting")] [field: Space]
        [field: SerializeField]
        public int FPSCap { get; private set; } = 11; // [ms]

        [field: SerializeField]
        public int ScenesLoadingBudget { get; private set; } = 100;

        [field: SerializeField]
        public int AssetsLoadingBudget { get; private set; } = 50;

        public Dictionary<MemoryUsageStatus, float> MemoryThresholds { get; private set; } = new ()
        {
            { MemoryUsageStatus.Warning, 0.5f },
            { MemoryUsageStatus.Full, 0.95f },
        };

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
