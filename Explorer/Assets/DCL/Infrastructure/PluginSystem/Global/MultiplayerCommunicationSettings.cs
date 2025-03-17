using DCL.Multiplayer.Movement.Settings;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class MultiplayerCommunicationSettings : IDCLPluginSettings
    {
        [field: Header(nameof(MultiplayerCommunicationSettings))] [field: Space]
        [field: SerializeField]
        internal MultiplayerCommunicationSettingsRef spatialStateSettings { get; private set; }

        [Serializable]
        public class MultiplayerCommunicationSettingsRef : AssetReferenceT<MultiplayerMovementSettings>
        {
            public MultiplayerCommunicationSettingsRef(string guid) : base(guid) { }
        }
    }
}
