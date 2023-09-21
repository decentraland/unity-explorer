using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.PluginSystem.Global;
using ECS;
using ECS.StreamableLoading.Cache;
using SceneRunner.Scene;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPluginWithoutSettings
    {
        //Should be taken from the catalyst
        private static readonly URLSubdirectory EXPLORER_SUBDIRECTORY = URLSubdirectory.FromString("/explorer/");
        private static readonly URLSubdirectory WEARABLES_COMPLEMENT_URL = URLSubdirectory.FromString("/wearables/");

        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleURL;

        private readonly WearableCatalog wearableCatalog;

        public WearablePlugin(IRealmData realmData, URLDomain assetBundleURL)
        {
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;
            wearableCatalog = new WearableCatalog();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            ResolveWearableByPointerSystem.InjectToWorld(ref builder, wearableCatalog, realmData);

            //ResolveWearableByParamSystem.InjectToWorld(ref builder, wearableCatalog);

            LoadWearablesByParamSystem.InjectToWorld(ref builder, new NoCache<IWearable[], GetWearableByParamIntention>(false, false), realmData, EXPLORER_SUBDIRECTORY, WEARABLES_COMPLEMENT_URL, wearableCatalog, mutexSync);
            LoadWearablesDTOByPointersSystem.InjectToWorld(ref builder, new NoCache<WearableDTO[], GetWearableDTOByPointersIntention>(false, false), mutexSync);
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(true, true), mutexSync, assetBundleURL);
        }
    }
}
