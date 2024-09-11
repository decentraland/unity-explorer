using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
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
using DCL.UI.MainUI;
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
using CharacterEmoteSystem = DCL.AvatarRendering.Emotes.Play.CharacterEmoteSystem;
using LoadAudioClipGlobalSystem = DCL.AvatarRendering.Emotes.Load.LoadAudioClipGlobalSystem;
using LoadEmotesByPointersSystem = DCL.AvatarRendering.Emotes.Load.LoadEmotesByPointersSystem;
using LoadOwnedEmotesSystem = DCL.AvatarRendering.Emotes.Load.LoadOwnedEmotesSystem;
using LoadSceneEmotesSystem = DCL.AvatarRendering.Emotes.Load.LoadSceneEmotesSystem;

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
        private readonly ISelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly DCLInput dclInput;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly AudioClipsCache audioClipsCache;
        private readonly URLDomain assetBundleURL;
        private readonly MainUIView mainUIView;
        private readonly ICursor cursor;
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private AudioSource? audioSourceReference;
        private EmotesWheelController? emotesWheelController;

        public EmotePlugin(IWebRequestController webRequestController,
            IEmoteStorage emoteStorage,
            IRealmData realmData,
            IEmotesMessageBus messageBus,
            IDebugContainerBuilder debugBuilder,
            IAssetsProvisioner assetsProvisioner,
            ISelfProfile selfProfile,
            IMVCManager mvcManager,
            DCLInput dclInput,
            CacheCleaner cacheCleaner,
            IWeb3IdentityCache web3IdentityCache,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            URLDomain assetBundleURL,
            MainUIView mainUIView,
            ICursor cursor,
            IInputBlock inputBlock,
            Arch.Core.World world,
            Entity playerEntity)
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
            this.emoteStorage = emoteStorage;
            this.realmData = realmData;
            this.mainUIView = mainUIView;
            this.cursor = cursor;
            this.world = world;
            this.playerEntity = playerEntity;
            this.inputBlock = inputBlock;

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
                new NoCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention>(false, false));

            LoadOwnedEmotesSystem.InjectToWorld(ref builder, realmData, webRequestController,
                new NoCache<EmotesResolution, GetOwnedEmotesFromRealmIntention>(false, false),
                emoteStorage);

            CharacterEmoteSystem.InjectToWorld(ref builder, emoteStorage, messageBus, audioSourceReference, debugBuilder);

            LoadAudioClipGlobalSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController);

            RemoteEmotesSystem.InjectToWorld(ref builder, web3IdentityCache, entityParticipantTable, messageBus, arguments.PlayerEntity);

            LoadSceneEmotesSystem.InjectToWorld(ref builder, emoteStorage, realmData, customStreamingSubdirectory);
        }

        public async UniTask InitializeAsync(EmoteSettings settings, CancellationToken ct)
        {
            EmbeddedEmotesData embeddedEmotesData = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmbeddedEmotes, ct)).Value;

            // TODO: convert into an async operation so we dont increment the loading times at app's startup
            IEnumerable<IEmote> embeddedEmotes = embeddedEmotesData.GenerateEmotes();

            audioSourceReference = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteAudioSource, ct)).Value.GetComponent<AudioSource>();

            foreach (IEmote embeddedEmote in embeddedEmotes)
                emoteStorage.Set(embeddedEmote.GetUrn(), embeddedEmote);

            var persistentEmoteWheelOpenerController = new PersistentEmoteWheelOpenerController(() => mainUIView.SidebarView.PersistentEmoteWheelOpener, mvcManager);

            mvcManager.RegisterController(persistentEmoteWheelOpenerController);

            EmotesWheelView emotesWheelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmotesWheelPrefab, ct))
                                               .Value.GetComponent<EmotesWheelView>();

            NftTypeIconSO emoteWheelRarityBackgrounds = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteWheelRarityBackgrounds, ct)).Value;

            IThumbnailProvider thumbnailProvider = new ECSThumbnailProvider(realmData, world, assetBundleURL, webRequestController);

            emotesWheelController = new EmotesWheelController(EmotesWheelController.CreateLazily(emotesWheelPrefab, null),
                selfProfile, emoteStorage, emoteWheelRarityBackgrounds, world, playerEntity, thumbnailProvider,
                inputBlock, dclInput, mvcManager, cursor);

            mvcManager.RegisterController(emotesWheelController);
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
