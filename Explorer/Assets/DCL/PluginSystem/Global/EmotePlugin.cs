using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Components;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Emotes.Interfaces;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.Multithreading;
using CharacterEmoteSystem = DCL.AvatarRendering.Emotes.CharacterEmoteSystem;

namespace DCL.PluginSystem.Global
{
    public class EmotePlugin : IDCLGlobalPlugin<EmotePlugin.EmoteSettings>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;
        private readonly IEmotesMessageBus messageBus;
        private readonly DebugContainerBuilder debugBuilder;

        public EmotePlugin(IWebRequestController webRequestController, MemoryEmotesCache emoteCache, RealmData realmData, IEmotesMessageBus messageBus, DebugContainerBuilder debugBuilder)
        {
            this.messageBus = messageBus;
            this.debugBuilder = debugBuilder;
            this.webRequestController = webRequestController;
            this.emoteCache = emoteCache;
            this.realmData = realmData;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
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

            CharacterEmoteSystem.InjectToWorld(ref builder, emoteCache, messageBus, debugBuilder);
        }

        public async UniTask InitializeAsync(EmoteSettings settings, CancellationToken ct)
        {
            IEnumerable<IEmote> embeddedEmotes = settings.EmbeddedEmotes.editorAsset.GenerateEmotes();
            AudioSource? audioSourceReference = settings.EmoteAudioSource.editorAsset;

            foreach (IEmote embeddedEmote in embeddedEmotes)
                emoteCache.Set(embeddedEmote.GetUrn(), embeddedEmote);
        }

        [Serializable]
        public class EmoteSettings : IDCLPluginSettings
        {
            [field: SerializeField] public EmbeddedEmotesReference EmbeddedEmotes { get; set; } = null!;
            [field: SerializeField] public EmoteAudioSourceReference EmoteAudioSource { get; set; } = null!;

            [Serializable]
            public class EmbeddedEmotesReference : AssetReferenceT<EmbeddedEmotesData>
            {
                public EmbeddedEmotesReference(string guid) : base(guid) { }
            }

            [Serializable]
            public class EmoteAudioSourceReference : AssetReferenceT<AudioSource>
            {
                public EmoteAudioSourceReference(string guid) : base(guid) { }
            }
        }
    }
}
