using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Thumbnails.Systems;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.AvatarRendering.Wearables.Systems.Load;
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

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPluginWithoutSettings
    {
        //Should be taken from the catalyst
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
            LoadTrimmedWearablesByParamSystem.InjectToWorld(ref builder, webRequestController,
                new NoCache<TrimmedWearablesResponse, GetTrimmedWearableByParamIntention>(false, false),
                realmData, WEARABLES_COMPLEMENT_URL, urlsSource, wearableStorage,
                trimmedWearableStorage, builderContentURL);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, webRequestController, new NoCache<WearablesDTOList, GetWearableDTOByPointersIntention>(false, false), entitiesAnalytics);
            LoadDefaultWearablesSystem.InjectToWorld(ref builder, wearableStorage);

            FinalizeAssetBundleWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);
            if (builderCollectionsPreview)
                FinalizeRawWearableLoadingSystem.InjectToWorld(ref builder, wearableStorage, realmData);

            ResolveAvatarAttachmentThumbnailSystem.InjectToWorld(ref builder);
            ResolveWearablePromisesSystem.InjectToWorld(ref builder, wearableStorage, urlsSource, WEARABLES_EMBEDDED_SUBDIRECTORY);
        }

    }
}
