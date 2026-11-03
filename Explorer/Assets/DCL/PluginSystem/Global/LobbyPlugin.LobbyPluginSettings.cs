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
            public LobbyDocumentViewRef DocumentPrefab { get; private set; }

            [field: SerializeField]
            public LobbyPopupsViewRef PopupsPrefab { get; private set; }

            [field: SerializeField]
            public LobbyStageRef StagePrefab { get; private set; }

            [field: Tooltip("Stage looks offered in Settings > Lobby, all loaded at startup for the picker thumbnails. The first entry is the default and should match the look serialized in the stage prefab; a seasonal look placed first must be moved down once the season ends. The choice is stored by name, so reordering keeps it")]
            [field: SerializeField]
            public StagePresetEntry[] StagePresets { get; private set; }

            [field: SerializeField]
            public LobbyAvatarSettings AvatarSettings { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypes { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationDefaultThumbnails> NotificationDefaultThumbnails { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }

            [Serializable]
            public class LobbyDocumentViewRef : ComponentReference<LobbyDocumentView>
            {
                public LobbyDocumentViewRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class LobbyPopupsViewRef : ComponentReference<LobbyPopupsView>
            {
                public LobbyPopupsViewRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class LobbyStageRef : ComponentReference<LobbyStage>
            {
                public LobbyStageRef(string guid) : base(guid) { }
            }

            [Serializable]
            public class StagePresetEntry
            {
                public string Name = string.Empty;
                public AssetReferenceT<LobbyStagePreset> Preset = null!;
            }
        }
    }
}
