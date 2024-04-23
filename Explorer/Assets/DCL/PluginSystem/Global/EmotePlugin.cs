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
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.Profiles.Self;
using DCL.WebRequests;
using ECS;
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
        private AudioSource? audioSourceReference;
        private EmotesWheelController? emotesWheelController;

        public EmotePlugin(IWebRequestController webRequestController,
            IEmoteCache emoteCache,
            IRealmData realmData,
            IEmotesMessageBus messageBus,
            DebugContainerBuilder debugBuilder,
            IAssetsProvisioner assetsProvisioner,
            ISelfProfile selfProfile,
            IMVCManager mvcManager,
            DCLInput dclInput)
        {
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.assetsProvisioner = assetsProvisioner;
            this.selfProfile = selfProfile;
            this.mvcManager = mvcManager;
            this.dclInput = dclInput;
            this.webRequestController = webRequestController;
            this.emoteCache = emoteCache;
            this.realmData = realmData;
        }

        public override void Dispose()
        {
            base.Dispose();

            emotesWheelController?.Dispose();
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var mutexSync = new MutexSync();

            LoadEmotesByPointersSystem.InjectToWorld(ref builder, webRequestController,
                new NoCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention>(false, false),
                mutexSync,
                emoteCache, realmData,
                URLSubdirectory.FromString("/Emotes/"));

            LoadOwnedEmotesSystem.InjectToWorld(ref builder, realmData, webRequestController,
                new NoCache<EmotesResolution, GetOwnedEmotesFromRealmIntention>(false, false),
                emoteCache, mutexSync);

            CharacterEmoteSystem.InjectToWorld(ref builder, emoteCache, messageBus, audioSourceReference, debugBuilder);
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(EmoteSettings settings, CancellationToken ct)
        {
            EmbeddedEmotesData embeddedEmotesData = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmbeddedEmotes, ct)).Value;

            // TODO: convert into an async operation so we dont increment the loading times at app's startup
            IEnumerable<IEmote> embeddedEmotes = embeddedEmotesData.GenerateEmotes();

            audioSourceReference = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteAudioSource, ct)).Value.GetComponent<AudioSource>();

            foreach (IEmote embeddedEmote in embeddedEmotes)
                emoteCache.Set(embeddedEmote.GetUrn(), embeddedEmote);

            EmotesWheelView emotesWheelPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmotesWheelPrefab, ct))
                                               .Value.GetComponent<EmotesWheelView>();

            NftTypeIconSO emoteWheelRarityBackgrounds = (await assetsProvisioner.ProvideMainAssetAsync(settings.EmoteWheelRarityBackgrounds, ct)).Value;

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                IThumbnailProvider thumbnailProvider = new ECSThumbnailProvider(realmData, builder.World);

                emotesWheelController = new EmotesWheelController(EmotesWheelController.CreateLazily(emotesWheelPrefab, null),
                    selfProfile, emoteCache, emoteWheelRarityBackgrounds, builder.World, arguments.PlayerEntity, thumbnailProvider,
                    builder.World.CacheInputMap(), dclInput.EmoteWheel, mvcManager);

                mvcManager.RegisterController(emotesWheelController);
            };
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
        }
    }
}
