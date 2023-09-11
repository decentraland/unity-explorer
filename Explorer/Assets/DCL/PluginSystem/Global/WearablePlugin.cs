using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.PluginSystem.Global;
using ECS.StreamableLoading.Cache;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Wearables
{
    public class WearablePlugin : IDCLGlobalPluginWithoutSettings
    {
        public static readonly string STREAMING_ASSETS_URL =
#if UNITY_EDITOR || UNITY_STANDALONE
            $"file://{Application.streamingAssetsPath}/AssetBundles/";
#else
            return $"{Application.streamingAssetsPath}/AssetBundles/";
#endif

        private readonly string AB_ASSETS_URL = "https://ab-cdn.decentraland.org/";

        //Should be taken from the catalyst
        private readonly string CATALYST_URL;
        private readonly string ENTITIES_ACTIVE;
        private readonly string EXPLORER_LAMBDA_URL = "/explorer";
        private readonly string CONTENT_URL = "/content";

        //TODO: Create a cache for the catalog
        private readonly Dictionary<string, Wearable> wearableCatalog;

        public WearablePlugin(string catalystURL, string entitiesActiveURL)
        {
            CATALYST_URL = catalystURL;
            ENTITIES_ACTIVE = entitiesActiveURL;
            wearableCatalog = new Dictionary<string, Wearable>();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            ResolveWearableByPointerIntentionSystem.InjectToWorld(ref builder, wearableCatalog, $"{CATALYST_URL}{ENTITIES_ACTIVE}");
            ResolveWearableByParamIntentionSystem.InjectToWorld(ref builder, wearableCatalog);

            LoadWearablesByParamSystem.InjectToWorld(ref builder, new NoCache<WearableDTO[], GetWearableDTOByParamIntention>(false, false), mutexSync, $"{CATALYST_URL}{EXPLORER_LAMBDA_URL}");
            LoadWearablesByPointersSystem.InjectToWorld(ref builder, new NoCache<WearableDTO[], GetWearableDTOByPointersIntention>(false, false), mutexSync);
            LoadWearableAssetBundleManifestSystem.InjectToWorld(ref builder, new NoCache<SceneAssetBundleManifest, GetWearableAssetBundleManifestIntention>(false, true), mutexSync, AB_ASSETS_URL);

            //LoadDefaultWearablesSystem.InjectToWorld(ref builder, $"{CATALYST_URL}{ENTITIES_ACTIVE}");
        }
    }
}
