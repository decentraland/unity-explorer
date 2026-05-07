using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.FeatureFlags;
using DCL.PluginSystem;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Owns shared emote dependencies (currently the <see cref="Play.EmotePlayer" /> instance and the
    ///     audio source asset). Created once at static-container initialization so both the global
    ///     <c>EmotePlugin</c> and the world-side <c>SceneMaskedEmotePlugin</c> consume the same concrete
    ///     instance via the container — no <c>ObjectProxy</c> bridging.
    /// </summary>
    public class EmotesContainer : DCLGlobalContainer<EmotesContainer.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        public EmotePlayer EmotePlayer { get; private set; } = null!;

        public EmotesContainer(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        protected override async UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            AudioSource audioSource = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteAudioSource, ct)).Value.GetComponent<AudioSource>();
            EmoteMaskCatalog emoteMaskCatalog = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteMaskCatalog, ct)).Value;

            bool legacyAnimationsEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.LOCAL_SCENE_DEVELOPMENT)
                                           || FeaturesRegistry.Instance.IsEnabled(FeatureId.SELF_PREVIEW_BUILDER_COLLECTIONS);

            EmotePlayer = new EmotePlayer(audioSource, emoteMaskCatalog, legacyAnimationsEnabled);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceGameObject EmoteAudioSource { get; set; } = null!;
            [field: SerializeField] public AssetReferenceT<EmoteMaskCatalog> EmoteMaskCatalog { get; set; } = null!;
        }
    }
}
