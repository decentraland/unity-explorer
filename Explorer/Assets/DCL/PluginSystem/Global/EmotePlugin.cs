using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.Multithreading;

namespace DCL.PluginSystem.Global
{
    public class EmotePlugin : IDCLGlobalPlugin<EmotePlugin.EmoteSettings>
    {
        private readonly IWebRequestController webRequestController;
        private readonly IEmoteCache emoteCache;
        private readonly IRealmData realmData;

        public EmotePlugin(IWebRequestController webRequestController,
            IEmoteCache emoteCache, IRealmData realmData)
        {
            this.webRequestController = webRequestController;
            this.emoteCache = emoteCache;
            this.realmData = realmData;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadEmotesByPointersSystem.InjectToWorld(ref builder, webRequestController,
                new NoCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention>(false, false),
                new MutexSync(),
                emoteCache, realmData);
        }

        public async UniTask InitializeAsync(EmoteSettings settings, CancellationToken ct) { }

        [Serializable]
        public class EmoteSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public EmbeddedEmotesReference EmbeddedEmotes { get; set; } = null!;

            [Serializable]
            public class EmbeddedEmotesReference : AssetReferenceT<EmbedEmotesData>
            {
                public EmbeddedEmotesReference(string guid) : base(guid) { }
            }
        }
    }
}
