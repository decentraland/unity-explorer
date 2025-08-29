using DCL.Landscape.Settings;
using DCL.Multiplayer.Movement.Settings;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class MultiplayerCommunicationSettings : IDCLPluginSettings
    {
        [field: Header(nameof(MultiplayerCommunicationSettings))] [field: Space]
        [field: SerializeField]
        internal MultiplayerMovementSettings MovementSettings { get; private set; }

        [field: SerializeField]
        internal LandscapeDataRef LandscapeData { get; private set; }
    }
}
