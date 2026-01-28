using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Systems;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.DebugUtilities;
using DCL.EmotesWheel;
using DCL.Input;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles.Self;
using DCL.ResourcesUnloading;
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
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
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
        private readonly SelfProfile selfProfile;
        private readonly IMVCManager mvcManager;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly AudioClipsCache audioClipsCache;
        private readonly string builderContentURL;
        private readonly ICursor cursor;
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly bool localSceneDevelopment;
        private readonly bool builderCollectionsPreview;
        private readonly IAppArgs appArgs;
        private readonly IThumbnailProvider thumbnailProvider;
        private readonly IScenesCache scenesCache;
        private readonly DCLInput dclInput;
        private readonly EntitiesAnalytics entitiesAnalytics;

        private AudioSource? audioSourceReference;
        private EmotesWheelController? emotesWheelController;

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
            bool builderCollectionsPreview,
            IAppArgs appArgs,
            IThumbnailProvider thumbnailProvider,
            IScenesCache scenesCache,
            EntitiesAnalytics entitiesAnalytics)
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
            this.builderCollectionsPreview = builderCollectionsPreview;
            this.appArgs = appArgs;
            this.thumbnailProvider = thumbnailProvider;
            this.scenesCache = scenesCache;
            this.dclInput = DCLInput.Instance;
            this.entitiesAnalytics = entitiesAnalytics;

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
                emoteStorage, realmData, customStreamingSubdirectory, entitiesAnalytics);

            LoadOwnedEmotesSystem.InjectToWorld(ref builder, realmData, webRequestController,
                new NoCache<EmotesResolution, GetOwnedEmotesFromRealmIntention>(false, false),
                emoteStorage, builderContentURL);

            if(builderCollectionsPreview)
                ResolveBuilderEmotePromisesSystem.InjectToWorld(ref builder, emoteStorage);

            CharacterEmoteSystem.InjectToWorld(ref builder, emoteStorage, messageBus, audioSourceReference, debugBuilder, localSceneDevelopment, appArgs, scenesCache);

            LoadAudioClipGlobalSystem.InjectToWorld(ref builder, audioClipsCache, webRequestController);

            RemoteEmotesSystem.InjectToWorld(ref builder, entityParticipantTable, messageBus);

            LoadSceneEmotesSystem.InjectToWorld(ref builder, emoteStorage, customStreamingSubdirectory);
        }

        public async UniTask InitializeAsync(EmoteSettings settings, CancellationToken ct)
        {
            IReadOnlyCollection<URN> baseEmotesUrns = settings.BaseEmotesAsURN();

            // Initialize the embedded emote URN mapping for legacy emote conversion
            EmoteComponentsUtils.InitializeLegacyToOnChainEmoteMapping(baseEmotesUrns);

            // Set default emotes (used in case of empty emote wheel)
            emoteStorage.SetBaseEmotesUrns(baseEmotesUrns);

            EmbeddedEmotesData embeddedEmotesData = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmbeddedEmotes, ct)).Value;

            // TODO: convert into an async operation so we don't increment the loading times at app's startup
            IEnumerable<IEmote> embeddedEmotes = embeddedEmotesData.GenerateEmotes();

            audioSourceReference = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteAudioSource, ct)).Value.GetComponent<AudioSource>();

            foreach (IEmote embeddedEmote in embeddedEmotes)
                emoteStorage.Set(embeddedEmote.GetUrn(), embeddedEmote);

            EmotesWheelView emotesWheelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmotesWheelPrefab, ct))
                                               .Value.GetComponent<EmotesWheelView>();

            NftTypeIconSO emoteWheelRarityBackgrounds = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteWheelRarityBackgrounds, ct)).Value;

            emotesWheelController = new EmotesWheelController(EmotesWheelController.CreateLazily(emotesWheelPrefab, null),
                selfProfile, emoteStorage, emoteWheelRarityBackgrounds, world, playerEntity, this.thumbnailProvider,
                inputBlock, cursor, mvcManager);

            mvcManager.RegisterController(emotesWheelController);

            dclInput.Shortcuts.EmoteWheel.canceled += OnEmoteWheelShortcutPerformed;
        }

        private void OnEmoteWheelShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (emotesWheelController?.State is ControllerState.ViewHidden)
                mvcManager.ShowAndForget(EmotesWheelController.IssueCommand());
            else
                emotesWheelController?.Close();
        }

        [Serializable]
        public class EmoteSettings : IDCLPluginSettings
        {
            [field: SerializeField] public AssetReferenceT<EmbeddedEmotesData> EmbeddedEmotes { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject EmoteAudioSource { get; set; } = null!;
            [field: SerializeField] public AssetReferenceGameObject EmotesWheelPrefab { get; set; } = null!;
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

            /// <summary>
            /// Ordered list of base emote URNs.
            /// The order defines the default emote order for users with no equipped emotes.
            /// </summary>
            [field: SerializeField]
            public string[] BaseEmotes { get; private set; }

            public IReadOnlyCollection<URN> BaseEmotesAsURN() =>
                BaseEmotes.Select(s => new URN(s)).ToArray();
        }
    }
}
