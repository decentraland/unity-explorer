using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Thumbnails.Systems;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using System;
using System.Threading;
using UnityEngine;
using LoadDefaultWearablesSystem = DCL.AvatarRendering.Wearables.Systems.LoadDefaultWearablesSystem;
using LoadWearablesByParamSystem = DCL.AvatarRendering.Wearables.Systems.LoadWearablesByParamSystem;
using LoadWearablesDTOByPointersSystem = DCL.AvatarRendering.Wearables.Systems.LoadWearablesDTOByPointersSystem;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPlugin<WearablePlugin.Settings>
    {
        //Should be taken from the catalyst
        private static readonly URLSubdirectory EXPLORER_SUBDIRECTORY = URLSubdirectory.FromString("/explorer/");
        private static readonly URLSubdirectory WEARABLES_COMPLEMENT_URL = URLSubdirectory.FromString("/wearables/");
        private static readonly URLSubdirectory WEARABLES_EMBEDDED_SUBDIRECTORY = URLSubdirectory.FromString("/Wearables/");
        private readonly string builderContentURL;
        private readonly IWebRequestController webRequestController;
        private readonly bool builderCollectionsPreview;
        private readonly IRealmData realmData;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWearableStorage wearableStorage;
        private readonly ITrimmedWearableStorage trimmedWearableStorage;
        private readonly EntitiesAnalytics entitiesAnalytics;

        private TimeSpan batchHeartbeat;

        public WearablePlugin(IWebRequestController webRequestController,
            IRealmData realmData,
            IDecentralandUrlsSource urlsSource,
            CacheCleaner cacheCleaner,
            IWearableStorage wearableStorage,
            ITrimmedWearableStorage trimmedWearableStorage,
            EntitiesAnalytics entitiesAnalytics,
            string builderContentURL,
            bool builderCollectionsPreview)
        {
            this.wearableStorage = wearableStorage;
            this.trimmedWearableStorage = trimmedWearableStorage;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.urlsSource = urlsSource;
            this.builderContentURL = builderContentURL;
            this.builderCollectionsPreview = builderCollectionsPreview;
            this.entitiesAnalytics = entitiesAnalytics;

            cacheCleaner.Register(this.wearableStorage);
            cacheCleaner.Register(this.trimmedWearableStorage);
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            LoadWearablesByParamSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesResponse, GetWearableByParamIntention>(false, false), realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableStorage, trimmedWearableStorage, urlsSource, builderContentURL);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesDTOList, GetWearableDTOByPointersIntention>(false, false), entitiesAnalytics);
            BatchWearablesDTOSystem.InjectToWorld(ref builder, urlsSource, batchHeartbeat);
            LoadDefaultWearablesSystem.InjectToWorld(ref builder, wearableStorage);

            FinalizeAssetBundleWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);

            if (builderCollectionsPreview)
                FinalizeRawWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);

            ResolveAvatarAttachmentThumbnailSystem.InjectToWorld(ref builder);
            ResolveWearablePromisesSystem.InjectToWorld(ref builder, wearableStorage, urlsSource, WEARABLES_EMBEDDED_SUBDIRECTORY);
        }

        UniTask IDCLPlugin<Settings>.InitializeAsync(Settings settings, CancellationToken ct)
        {
            batchHeartbeat = TimeSpan.FromMilliseconds(settings.BatchHeartbeatMs);
            return UniTask.CompletedTask;
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public uint BatchHeartbeatMs { get; private set; } = 100;
        }
    }
}
