using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using Global;
using Global.Dynamic;
using MVC;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using DCL.Browser.DecentralandUrls;
using DCL.FeatureFlags;
using DCL.Landscape;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PrivateWorlds;
using DCL.Prefs;
using Global.AppArgs;
using Unity.Mathematics;
using UnityEngine;
using Utility.Storage;

namespace DCL.RealmNavigation
{
    public class RealmContainer
    {
        public TeleportController TeleportController { get; private set; }
        public IGlobalRealmController RealmController { get; private set; }
        public RealmNavigatorDebugView DebugView { get; private set; }
        public LoadingScreenTimeout LoadingScreenTimeout { get; private set; }
        public ILoadingScreen LoadingScreen { get; private set; }
        public ECSReloadScene ReloadSceneController { get; private set; }

        public static RealmContainer Create(
            StaticContainer staticContainer,
            IReadOnlyList<int2> staticLoadPositions,
            IDebugContainerBuilder debugContainerBuilder,
            IMVCManager mvcManager,
            bool localSceneDevelopment,
            IDecentralandUrlsSource urlsSource,
            IAppArgs appArgs,
            DecentralandEnvironment dclEnvironment,
            World globalWorld,
            Entity playerEntity)
        {
            var teleportController = new TeleportController(staticContainer.SceneReadinessReportQueue);

            var reloadSceneController = new ECSReloadScene(staticContainer.ScenesCache, globalWorld, playerEntity, localSceneDevelopment, staticContainer.CacheCleaner);

            var loadingScreenTimeout = new LoadingScreenTimeout();
            ILoadingScreen loadingScreen = new LoadingScreen(mvcManager, loadingScreenTimeout);

            var retrieveSceneFromFixedRealm = new RetrieveSceneFromFixedRealm();
            var retrieveSceneFromVolatileWorld = new RetrieveSceneFromVolatileWorld(staticContainer.RealmData, urlsSource);

            var realmNavigatorDebugView = new RealmNavigatorDebugView(debugContainerBuilder);

            var realmController = new RealmController(
                staticContainer.WebRequestsContainer.WebRequestController,
                teleportController,
                retrieveSceneFromFixedRealm,
                retrieveSceneFromVolatileWorld,
                staticLoadPositions,
                staticContainer.RealmData,
                staticContainer.ScenesCache,
                staticContainer.PartitionDataContainer,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry
                               .GetReferenceTypePool<PartitionComponent>(),
                realmNavigatorDebugView,
                localSceneDevelopment,
                appArgs,
                urlsSource,
                dclEnvironment,
                staticContainer.WorldManifestProvider
            );

            BuildDebugWidget(teleportController, debugContainerBuilder, loadingScreen, loadingScreenTimeout);

            return new RealmContainer
            {
                RealmController = realmController,
                DebugView = realmNavigatorDebugView,
                TeleportController = teleportController,
                LoadingScreenTimeout = loadingScreenTimeout,
                LoadingScreen = loadingScreen,
                ReloadSceneController = reloadSceneController,
            };
        }

        private static void BuildDebugWidget(ITeleportController teleportController, IDebugContainerBuilder debugContainerBuilder, ILoadingScreen loadingScreen, LoadingScreenTimeout loadingScreenTimeout)
        {
            var binding = new PersistentElementBinding<Vector2Int>(PersistentSetting.CreateVector2Int(DCLPrefKeys.DEBUG_TELEPORT_COORDINATES));

            var timeout = new ElementBinding<float>((float)loadingScreenTimeout.Value.TotalSeconds,
                evt => loadingScreenTimeout.Set(seconds: evt.newValue));

            debugContainerBuilder
               .TryAddWidget("Teleport")
              ?.AddControl(new DebugVector2IntFieldDef(binding), null)
               .AddControl(
                    new DebugButtonDef("To Parcel", () =>
                        {
                            loadingScreen.ShowWhileExecuteTaskAsync(
                                              (report, token) => teleportController.TeleportToParcelAsync(binding.Value, report, token).SuppressToResultAsync()
                                            , CancellationToken.None)
                                         .Forget();
                        }
                    ),
                    new DebugButtonDef("To Spawn Point", () =>
                        {
                            loadingScreen.ShowWhileExecuteTaskAsync(
                                              async (report, token) =>
                                              {
                                                  WaitForSceneReadiness? sceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(binding.Value, report, token);
                                                  return await sceneReadiness.ToUniTask();
                                              }, CancellationToken.None)
                                         .Forget();
                        }
                    )
                )
               .AddFloatField("Timeout (s)", timeout);
        }
    }
}
