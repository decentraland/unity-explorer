﻿using DCL.LOD;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization;
using System;
using System.Collections.Generic;
using DCL.Roads.Settings;
using DCL.AvatarRendering;
using DCL.SDKComponents.MediaStream.Settings;
using DCL.Settings.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class StaticSettings : IDCLPluginSettings
    {
        [field: Header(nameof(StaticSettings))] [field: Space]
        [field: SerializeField]
        public PartitionSettingsRef PartitionSettings { get; private set; }

        [field: SerializeField]
        public RealmPartitionSettingsRef RealmPartitionSettings { get; private set; }

        // Performance budgeting
        [field: Header("Performance Budgeting")] [field: Space]
        [field: SerializeField]
        private int frameTimeCap = 33; // in [ms]. Table: 33ms ~ 30fps | 16ms ~ 60fps | 11ms ~ 90 fps | 8ms ~ 120fps
        [SerializeField]
        private int frameTimeCapDeepProfiler = 300;

        public int FrameTimeCap
        {
            get
            {
                bool isDeepProfiling = Profiler.enabled && Profiler.enableBinaryLog;
                return isDeepProfiling ? frameTimeCapDeepProfiler : frameTimeCap;
            }
        }

        public Dictionary<MemoryUsageStatus, float> MemoryThresholds { get; private set; } = new ()
        {
            { MemoryUsageStatus.ABUNDANCE, 0.65f },
            { MemoryUsageStatus.WARNING, 0.7f },
            { MemoryUsageStatus.FULL, 0.75f }
        };

        [field: Space]
        [field: SerializeField] public int ScenesLoadingBudget { get; private set; } = 100;
        [field: SerializeField] public int AssetsLoadingBudget { get; private set; } = 50;
        [field: SerializeField] public int CoreWebRequestsBudget { get; private set; } = 15;
        [field: SerializeField] public int SceneWebRequestsBudget { get; private set; } = 5;


        [Serializable]
        public class PartitionSettingsRef : AssetReferenceT<PartitionSettingsAsset>
        {
            public PartitionSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class VoiceChatSettingsRef : AssetReferenceT<VoiceChatSettingsAsset>
        {
            public VoiceChatSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class RealmPartitionSettingsRef : AssetReferenceT<RealmPartitionSettingsAsset>
        {
            public RealmPartitionSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class VideoPrioritizationSettingsRef : AssetReferenceT<VideoPrioritizationSettings>
        {
            public VideoPrioritizationSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class LODSettingsRef : AssetReferenceT<LODSettingsAsset>
        {
            public LODSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class AvatarRandomizerSettingsRef : AssetReferenceT<AvatarRandomizerAsset>
        {
            public AvatarRandomizerSettingsRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class RoadDataRef : AssetReferenceT<RoadSettingsAsset>
        {
            public RoadDataRef(string guid) : base(guid) { }
        }
    }
}
