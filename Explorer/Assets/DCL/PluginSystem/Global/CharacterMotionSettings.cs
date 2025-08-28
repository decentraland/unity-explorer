using DCL.CharacterMotion.Settings;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class CharacterMotionSettings : IDCLPluginSettings
    {
        [field: Header(nameof(CharacterMotionSettings))] [field: Space]
        [field: SerializeField]
        internal CharacterControllerSettings controllerSettings { get; private set; }
    }
}
