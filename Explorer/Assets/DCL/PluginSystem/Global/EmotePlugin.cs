using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.DebugUtilities;
using DCL.EmotesWheel;
using DCL.Input;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.Multithreading;
using CharacterEmoteSystem = DCL.AvatarRendering.Emotes.CharacterEmoteSystem;

namespace DCL.PluginSystem.Global
{
    public class EmotePlugin : DCLGlobalPluginBase<EmotePlugin.EmoteSettings>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;
        private readonly IEmotesMessageBus messageBus;
        private readonly DebugContainerBuilder debugBuilder;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ISelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private AudioSource? audioSourceReference;
        private EmotesWheelController? emotesWheelController;
        private readonly AudioClipsCache audioClipsCache;
        private readonly URLDomain assetBundleURL;

        public EmotePlugin(IWebRequestController webRequestController,
            IEmoteCache emoteCache,
            IRealmData realmData,
            IEmotesMessageBus messageBus,
            DebugContainerBuilder debugBuilder,
            IAssetsProvisioner assetsProvisioner,
            ISelfProfile selfProfile,
            IMVCManager mvcManager,
            DCLInput dclInput,
            CacheCleaner cacheCleaner,
            IWeb3IdentityCache web3IdentityCache,
            IReadOnlyEntityParticipantTable entityParticipantTable, URLDomain assetBundleURL)
        {
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;
            this.web3IdentityCache = web3IdentityCache;
            this.entityParticipantTable = entityParticipantTable;
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
            this.emoteCache = emoteCache;
            this.realmData = realmData;

            audioClipsCache = new AudioClipsCache();
            cacheCleaner.Register(audioClipsCache);
        }

        public override void Dispose()
        {
            base.Dispose();

            emotesWheelController?.Dispose();
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var customStreamingSubdirectory = URLSubdirectory.FromString("/Emotes/");

            LoadEmotesByPointersSystem.InjectToWorld(ref builder, webRequestController,
                new NoCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention>(false, false),
                emoteCache, realmData,
                customStreamingSubdirectory);

            LoadOwnedEmotesSystem.InjectToWorld(ref builder, realmData, webRequestController,
                new NoCache<EmotesResolution, GetOwnedEmotesFromRealmIntention>(false, false),
                emoteCache);

            CharacterEmoteSystem.InjectToWorld(ref builder, emoteCache, messageBus, audioSourceReference, debugBuilder);

            LoadEmoteAudioClipSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController);

            RemoteEmotesSystem.InjectToWorld(ref builder, web3IdentityCache, entityParticipantTable, messageBus, arguments.PlayerEntity);

            LoadSceneEmotesSystem.InjectToWorld(ref builder, emoteCache, customStreamingSubdirectory);
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(EmoteSettings settings, CancellationToken ct)
        {
            EmbeddedEmotesData embeddedEmotesData = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmbeddedEmotes, ct)).Value;

            // TODO: convert into an async operation so we dont increment the loading times at app's startup
            IEnumerable<IEmote> embeddedEmotes = embeddedEmotesData.GenerateEmotes();

            audioSourceReference = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteAudioSource, ct)).Value.GetComponent<AudioSource>();

            foreach (IEmote embeddedEmote in embeddedEmotes)
                emoteCache.Set(embeddedEmote.GetUrn(), embeddedEmote);

            PersistentEmoteWheelOpenerView persistentEmoteWheelOpenerView = (await assetsProvisioner.ProvideMainAssetAsync(settings.PersistentEmoteWheelOpenerPrefab, ct))
                                                                           .Value.GetComponent<PersistentEmoteWheelOpenerView>();

            var persistentEmoteWheelOpenerController = new PersistentEmoteWheelOpenerController(
                PersistentEmoteWheelOpenerController.CreateLazily(persistentEmoteWheelOpenerView, null),
                mvcManager);

            mvcManager.RegisterController(persistentEmoteWheelOpenerController);

            EmotesWheelView emotesWheelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmotesWheelPrefab, ct))
                                               .Value.GetComponent<EmotesWheelView>();

            NftTypeIconSO emoteWheelRarityBackgrounds = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteWheelRarityBackgrounds, ct)).Value;

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                IThumbnailProvider thumbnailProvider = new ECSThumbnailProvider(realmData, builder.World, assetBundleURL, webRequestController);

                emotesWheelController = new EmotesWheelController(EmotesWheelController.CreateLazily(emotesWheelPrefab, null),
                    selfProfile, emoteCache, emoteWheelRarityBackgrounds, builder.World, arguments.PlayerEntity, thumbnailProvider,
                    builder.World.CacheInputMap(), dclInput, mvcManager);

                mvcManager.RegisterController(emotesWheelController);
            };
        }

        [Serializable]
        public class EmoteSettings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceT<EmbeddedEmotesData> EmbeddedEmotes { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject EmoteAudioSource { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject EmotesWheelPrefab { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject PersistentEmoteWheelOpenerPrefab { get; set; } = null!;
            [field: SerializeField] public AssetReferenceT<NftTypeIconSO> EmoteWheelRarityBackgrounds { get; set; } = null!;

            [Serializable]
            public class EmbeddedEmotesReference : AssetReferenceT<EmbeddedEmotesData>
            {
                public EmbeddedEmotesReference(string guid) : base(guid) { }
            }

            [Serializable]
            public class EmoteAudioSourceReference : AssetReferenceGameObject
            {
                public EmoteAudioSourceReference(string guid) : base(guid) { }
            }
        }
    }
}
