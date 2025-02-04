using DCL.AssetsProvision;
using DCL.Chat;
using DCL.Input;
using DCL.Multiplayer.Movement.Settings;
using DCL.Nametags;
using DCL.UI.Profiles.Helpers;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class DynamicSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceGameObject PopupCloserView { get; private set; }
        [field: SerializeField] public Light DirectionalLight { get; private set; }
        [field: SerializeField] public AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public ProfileNameColorHelperRef ProfileNameColorHelper { get; private set; }
        [field: SerializeField] public AssetReferenceT<NametagsData> NametagsData { get; private set; }
        [field: SerializeField] public AssetReferenceT<CursorSettings> CursorSettings { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject MainUIView { get; private set; }
        [field: SerializeField] public AssetReferenceT<AudioMixer> GeneralAudioMixer { get; private set; }
        [field: SerializeField] public AssetReferenceT<MultiplayerDebugSettings> MultiplayerDebugSettings { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject AppVerRedirectionScreenPrefab { get; private set; }


        [Serializable]
        public class ProfileNameColorHelperRef : AssetReferenceT<ProfileNameColorsConfigurationSO>
        {
            public ProfileNameColorHelperRef(string guid) : base(guid) { }
        }
    }
}
