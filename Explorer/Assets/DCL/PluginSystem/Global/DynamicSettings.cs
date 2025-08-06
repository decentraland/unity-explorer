using DCL.Communities.CommunitiesCard.Members;
using DCL.Input;
using DCL.Multiplayer.Movement.Settings;
using DCL.Nametags;
using DCL.UI.GenericContextMenu.Controllers;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.UI.Profiles.Helpers;
using System;
using System.Collections.Generic;
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
        [field: SerializeField] public AssetReferenceT<NametagsData> NametagsData { get; private set; }
        [field: SerializeField] public AssetReferenceT<CursorSettings> CursorSettings { get; private set; }
        [field: SerializeField] public AssetReferenceT<GenericUserProfileContextMenuSettings> GenericUserProfileContextMenuSettings { get; private set; }
        [field: SerializeField] public AssetReferenceT<CommunityVoiceChatContextMenuConfiguration> CommunityVoiceChatContextMenuSettings { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject MainUIView { get; private set; }
        [field: SerializeField] public AssetReferenceT<AudioMixer> GeneralAudioMixer { get; private set; }
        [field: SerializeField] public AssetReferenceT<MultiplayerDebugSettings> MultiplayerDebugSettings { get; private set; }
        [field: SerializeField] public AssetReferenceT<AdaptivePhysicsSettings> AdaptivePhysicsSettings { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject AppVerRedirectionScreenPrefab { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject BlockedScreenPrefab { get; private set; }
        [field: SerializeField] public AssetReferenceGameObject MinimumSpecsScreenPrefab { get; private set; }

        [field: SerializeField] public AssetReferenceGameObject LivekitDownPrefab { get; private set; }
        [field:SerializeField] public List<Color> UserNameColors { get; private set; }
    }
}
