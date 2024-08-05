using DCL.Chat;
using DCL.Input;
using DCL.MapRenderer;
using DCL.Nametags;
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
        [field: SerializeField] public AssetReferenceT<NametagsData> NametagsData { get; private set; }
        [field: SerializeField] public AssetReferenceT<CursorSettings> CursorSettings { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject MainUIView { get; private set; }
        [field: SerializeField] public AssetReferenceT<AudioMixer> GeneralAudioMixer { get; private set; }
    }
}
