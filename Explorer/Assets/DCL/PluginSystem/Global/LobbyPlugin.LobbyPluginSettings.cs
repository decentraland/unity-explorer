using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Lobby;
using DCL.Notifications;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

            [field: SerializeField]
            public LobbyStageRef StagePrefab { get; private set; }

            [field: SerializeField]
            public LobbyAvatarSettings AvatarSettings { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypes { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationDefaultThumbnails> NotificationDefaultThumbnails { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }

            [Serializable]
            public class LobbyViewRef : ComponentReference<LobbyView>
            {
                public LobbyViewRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class LobbyStageRef : ComponentReference<LobbyStage>
            {
                public LobbyStageRef(string guid) : base(guid) { }
            }
        }
    }
}
