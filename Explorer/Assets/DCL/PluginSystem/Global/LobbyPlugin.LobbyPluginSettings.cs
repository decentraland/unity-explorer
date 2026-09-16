using DCL.AssetsProvision;
using DCL.Lobby;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public partial class LobbyPlugin
    {
        [Serializable]
        public struct LobbyPluginSettings : IDCLPluginSettings
        {
            [field: Header(nameof(LobbyPlugin) + "." + nameof(LobbyPluginSettings))]
            [field: Space]
            [field: SerializeField]
            public LobbyViewRef LobbyPrefab { get; private set; }

            [Serializable]
            public class LobbyViewRef : ComponentReference<LobbyView>
            {
                public LobbyViewRef(string guid) : base(guid) { }
            }
        }
    }
}
