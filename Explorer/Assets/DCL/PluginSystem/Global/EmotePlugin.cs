using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.SocialEmotes;
using DCL.AvatarRendering.Emotes.Systems;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.DebugUtilities;
using DCL.EmotesWheel;
using DCL.Input;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.Profiles.Self;
using DCL.ResourcesUnloading;
using DCL.SocialEmotes.UI;
using DCL.UI.SharedSpaceManager;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using Global.AppArgs;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using ECS.SceneLifeCycle;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using CharacterEmoteSystem = DCL.AvatarRendering.Emotes.Play.CharacterEmoteSystem;
using LoadAudioClipGlobalSystem = DCL.AvatarRendering.Emotes.Load.LoadAudioClipGlobalSystem;
using LoadEmotesByPointersSystem = DCL.AvatarRendering.Emotes.Load.LoadEmotesByPointersSystem;
using LoadOwnedEmotesSystem = DCL.AvatarRendering.Emotes.Load.LoadOwnedEmotesSystem;
using LoadSceneEmotesSystem = DCL.AvatarRendering.Emotes.Load.LoadSceneEmotesSystem;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class EmotePlugin : IDCLGlobalPlugin<EmotePlugin.EmoteSettings>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IEmoteStorage emoteStorage;
        private readonly IRealmData realmData;
        private readonly IEmotesMessageBus messageBus;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly SelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly AudioClipsCache audioClipsCache;
        private readonly string builderContentURL;
        private readonly ICursor cursor;
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private AudioSource? audioSourceReference;
        private EmotesWheelController? emotesWheelController;
        private readonly bool localSceneDevelopment;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly bool builderCollectionsPreview;
        private readonly IAppArgs appArgs;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IScenesCache scenesCache;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        private Transform? socialPinsPoolParent = null;
        private Transform poolParent;
        private IObjectPool<SocialEmotePin> socialEmotePinsPool;

        public EmotePlugin(IWebRequestController webRequestController,
            IEmoteStorage emoteStorage,
            IRealmData realmData,
            IEmotesMessageBus messageBus,
            IDebugContainerBuilder debugBuilder,
            IAssetsProvisioner assetsProvisioner,
            SelfProfile selfProfile,
            IMVCManager mvcManager,
            CacheCleaner cacheCleaner,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ICursor cursor,
            IInputBlock inputBlock,
            Arch.Core.World world,
            Entity playerEntity,
            string builderContentURL,
            bool localSceneDevelopment,
            ISharedSpaceManager sharedSpaceManager,
            bool builderCollectionsPreview,
            IAppArgs appArgs,
            IThumbnailProvider thumbnailProvider,
            IScenesCache scenesCache,
            IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.entityParticipantTable = entityParticipantTable;
            this.builderContentURL = builderContentURL;
            this.webRequestController = webRequestController;
            this.emoteStorage = emoteStorage;
            this.realmData = realmData;
            this.cursor = cursor;
            this.world = world;
            this.playerEntity = playerEntity;
            this.inputBlock = inputBlock;
            this.localSceneDevelopment = localSceneDevelopment;
            this.sharedSpaceManager = sharedSpaceManager;
            this.builderCollectionsPreview = builderCollectionsPreview;
            this.appArgs = appArgs;
            this.thumbnailProvider = thumbnailProvider;
            this.scenesCache = scenesCache;
            this.componentPoolsRegistry = componentPoolsRegistry;

            audioClipsCache = new AudioClipsCache();
            cacheCleaner.Register(audioClipsCache);
        }

        public void Dispose()
        {
            emotesWheelController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var customStreamingSubdirectory = URLSubdirectory.FromString("/Emotes/");

            FinalizeEmoteLoadingSystem.InjectToWorld(ref builder, emoteStorage);

            LoadEmotesByPointersSystem.InjectToWorld(ref builder, webRequestController,
                new NoCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention>(false, false),
                emoteStorage, realmData, customStreamingSubdirectory);

            LoadOwnedEmotesSystem.InjectToWorld(ref builder, realmData, webRequestController,
                new NoCache<EmotesResolution, GetOwnedEmotesFromRealmIntention>(false, false),
                emoteStorage, builderContentURL);

            if(builderCollectionsPreview)
                ResolveBuilderEmotePromisesSystem.InjectToWorld(ref builder, emoteStorage);

            CharacterEmoteSystem.InjectToWorld(ref builder, emoteStorage, messageBus, audioSourceReference, debugBuilder, localSceneDevelopment, appArgs, scenesCache);;

            LoadAudioClipGlobalSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController);

            RemoteEmotesSystem.InjectToWorld(ref builder, entityParticipantTable, messageBus);

            LoadSceneEmotesSystem.InjectToWorld(ref builder, emoteStorage, customStreamingSubdirectory);

            SocialEmoteInteractionSystem.InjectToWorld(ref builder);

            SocialEmotePinsSystem.InjectToWorld(ref builder, socialEmotePinsPool);
        }

        public async UniTask InitializeAsync(EmoteSettings settings, CancellationToken ct)
        {
            EmbeddedEmotesData embeddedEmotesData = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmbeddedEmotes, ct)).Value;

            // TODO: convert into an async operation so we don't increment the loading times at app's startup
            IEnumerable<IEmote> embeddedEmotes = embeddedEmotesData.GenerateEmotes();

            audioSourceReference = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteAudioSource, ct)).Value.GetComponent<AudioSource>();

            foreach (IEmote embeddedEmote in embeddedEmotes)
                emoteStorage.AddEmbeded(embeddedEmote.GetUrn(), embeddedEmote);

            EmotesWheelView emotesWheelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmotesWheelPrefab, ct))
                                               .Value.GetComponent<EmotesWheelView>();

            NftTypeIconSO emoteWheelRarityBackgrounds = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteWheelRarityBackgrounds, ct)).Value;

            emotesWheelController = new EmotesWheelController(EmotesWheelController.CreateLazily(emotesWheelPrefab, null),
                selfProfile, emoteStorage, emoteWheelRarityBackgrounds, world, playerEntity, this.thumbnailProvider,
                inputBlock, cursor, sharedSpaceManager);

            sharedSpaceManager.RegisterPanel(PanelsSharingSpace.EmotesWheel, emotesWheelController);

            mvcManager.RegisterController(emotesWheelController);

            await CreateNametagPoolAsync(settings, ct);
        }

        private async UniTask CreateNametagPoolAsync(EmoteSettings settings, CancellationToken ct)
        {
            SocialEmotePin pinPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.SocialEmotePinPrefab, ct: ct)).Value;

            var poolRoot = componentPoolsRegistry.RootContainerTransform();
            poolParent = new GameObject("POOL_CONTAINER_SocialEmotePins").transform;
            poolParent.parent = poolRoot;

            socialEmotePinsPool = new ObjectPool<SocialEmotePin>(
                () =>
                {
                    SocialEmotePin socialEmotePin = UnityEngine.Object.Instantiate(pinPrefab, Vector3.zero, Quaternion.identity, poolParent);
                    socialEmotePin.gameObject.SetActive(false);
                    return socialEmotePin;
                },
                actionOnRelease: pin => pin.gameObject.SetActive(false),
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                actionOnGet: pin => pin.gameObject.SetActive(true));
        }

        [Serializable]
        public class EmoteSettings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceT<EmbeddedEmotesData> EmbeddedEmotes { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject EmoteAudioSource { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject EmotesWheelPrefab { get; set; } = null!;
            [field: SerializeField] public AssetReferenceT<NftTypeIconSO> EmoteWheelRarityBackgrounds { get; set; } = null!;
            [field: SerializeField] public SocialEmotePinRef SocialEmotePinPrefab { get; set; } = null!;

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

            [Serializable]
            public class SocialEmotePinRef : ComponentReference<SocialEmotePin>
            {
                public SocialEmotePinRef(string guid) : base(guid) { }
            }
        }
    }
}
