using DCL.AvatarRendering.Emotes;
using DCL.Backpack;
using DCL.Chat;
using DCL.MapRenderer;
using DCL.Nametags;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class DynamicSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AssetReferenceGameObject PopupCloserView { get; private set; }
        [field: SerializeField] public Light DirectionalLight { get; private set; }
        [field: SerializeField] public MapRendererSettings MapRendererSettings { get; private set; }
        [field: SerializeField] public BackpackSettings BackpackSettings { get; private set; }
        [field: SerializeField] public AssetReferenceT<ChatEntryConfigurationSO> ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public AssetReferenceT<NametagsData> NametagsData { get; private set; }
        [field: SerializeField] public AssetReferenceTexture2D NormalCursor { get; private set; }
        [field: SerializeField] public AssetReferenceTexture2D InteractionCursor { get; private set; }
    }
}
