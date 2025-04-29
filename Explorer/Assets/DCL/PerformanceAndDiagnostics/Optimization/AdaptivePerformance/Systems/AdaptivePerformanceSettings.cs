using DCL.PluginSystem;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [Serializable]
    public class AdaptivePerformanceSettings : IDCLPluginSettings
    {
        [field: Header(nameof(AdaptivePerformanceSettings))] [field: Space]
        [field: SerializeField] internal AdaptivePhysicsSettingsRef phyiscsSettings { get; private set; }

        [Serializable]
        public class AdaptivePhysicsSettingsRef : AssetReferenceT<AdaptivePhysicsSettings>
        {
            public AdaptivePhysicsSettingsRef(string guid) : base(guid) { }
        }
    }
}
