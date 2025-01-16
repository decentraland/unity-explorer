﻿using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.ParcelsService;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using Global;
using Global.Dynamic;
using System.Collections.Generic;
using System.Threading;
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

        public static RealmContainer Create(
            StaticContainer staticContainer,
            IWeb3IdentityCache identityCache,
            IReadOnlyList<int2> staticLoadPositions,
            IDebugContainerBuilder debugContainerBuilder,
            LoadingScreenTimeout loadingScreenTimeout,
            ILoadingScreen loadingScreen,
            bool localSceneDevelopment)
        {
            var teleportController = new TeleportController(staticContainer.SceneReadinessReportQueue, staticContainer.SingletonSharedDependencies.SceneAssetLock);

            var retrieveSceneFromFixedRealm = new RetrieveSceneFromFixedRealm();
            var retrieveSceneFromVolatileWorld = new RetrieveSceneFromVolatileWorld(staticContainer.RealmData);

            var realmNavigatorDebugView = new RealmNavigatorDebugView(debugContainerBuilder);

            var realmController = new RealmController(
                identityCache,
                staticContainer.WebRequestsContainer.WebRequestController,
                teleportController,
                retrieveSceneFromFixedRealm,
                retrieveSceneFromVolatileWorld,
                staticLoadPositions,
                staticContainer.RealmData,
                staticContainer.ScenesCache,
                staticContainer.PartitionDataContainer,
                staticContainer.SingletonSharedDependencies.SceneAssetLock,
                staticContainer.ComponentsContainer.ComponentPoolsRegistry
                               .GetReferenceTypePool<PartitionComponent>(),
                realmNavigatorDebugView,
                localSceneDevelopment
            );

            BuildDebugWidget(teleportController, debugContainerBuilder, loadingScreen, loadingScreenTimeout);

            return new RealmContainer
            {
                RealmController = realmController,
                DebugView = realmNavigatorDebugView,
                TeleportController = teleportController,
            };
        }

        private static void BuildDebugWidget(ITeleportController teleportController, IDebugContainerBuilder debugContainerBuilder, ILoadingScreen loadingScreen, LoadingScreenTimeout loadingScreenTimeout)
        {
            var binding = new PersistentElementBinding<Vector2Int>(PersistentSetting.CreateVector2Int("teleportCoordinates"));

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
                                                  return await sceneReadiness.ToUniTask().SuppressToResultAsync();
                                              }, CancellationToken.None)
                                         .Forget();
                        }
                    )
                )
               .AddFloatField("Timeout (s)", timeout);
        }
    }
}
