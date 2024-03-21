using DCL.AssetsProvision;
using DCL.Character;
using DCL.Diagnostics;
using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using DCL.Roads.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class StaticSettings : IDCLPluginSettings
    {
        [field: Header(nameof(StaticSettings))] [field: Space]
        [field: SerializeField]
        public ReportHandlingSettingsRef ReportHandlingSettings { get; private set; }

        [field: SerializeField]
        public PartitionSettingsRef PartitionSettings { get; private set; }

        [field: SerializeField]
        public RealmPartitionSettingsRef RealmPartitionSettings { get; private set; }

        // Performance budgeting
        [field: Header("Performance Budgeting")] [field: Space]
        [field: SerializeField]
        public int FrameTimeCap { get; private set; } = 33; // in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps

        [field: SerializeField]
        public int ScenesLoadingBudget { get; private set; } = 100;

        [field: SerializeField]
        public int AssetsLoadingBudget { get; private set; } = 50;

        public Dictionary<MemoryUsageStatus, float> MemoryThresholds { get; private set; } = new ()
        {
            { MemoryUsageStatus.Warning, 0.8f },
            { MemoryUsageStatus.Full, 0.95f },
        };

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

        [Serializable]
        public class LODSettingsRef : AssetReferenceT<LODSettingsAsset>
        {
            public LODSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class RoadDataRef : AssetReferenceT<RoadSettingsAsset>
        {
            public RoadDataRef(string guid) : base(guid) { }
        }
    }
}
