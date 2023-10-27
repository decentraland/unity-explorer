using DCL.DebugUtilities;
using DCL.PluginSystem;
using System;
using UnityEngine;

namespace Global.Dynamic
{
    [Serializable]
    public class DynamicWorldSettings : IDCLPluginSettings
    {
        [field: Header(nameof(DynamicWorldSettings))]
        [field: Space]
        [field: SerializeField]
        public DebugViewsCatalog DebugViewsCatalog { get; private set; }
    }
}
