using DCL.PluginSystem;
using System;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [Serializable]
    public class AdaptivePerformanceSettings : IDCLPluginSettings
    {
        [field: Header(nameof(AdaptivePerformanceSettings))] [field: Space]
        [field: SerializeField] internal AdaptivePhysicsSettings phyiscsSettings { get; private set; }
    }
}
