﻿using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.SceneLoadingScreens;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility.Storage;

namespace DCL.ParcelsService
{
    public class ParcelServiceContainer
    {
        public RetrieveSceneFromVolatileWorld RetrieveSceneFromVolatileWorld { get; private set; }
        public RetrieveSceneFromFixedRealm RetrieveSceneFromFixedRealm { get; private set; }
        public TeleportController TeleportController { get; private set; }

        public static ParcelServiceContainer Create(IRealmData realmData,
            ISceneReadinessReportQueue sceneReadinessReportQueue,
            IDebugContainerBuilder debugContainerBuilder,
            IMVCManager mvcManager,
            SceneAssetLock assetLock)
        {
            var teleportController = new TeleportController(sceneReadinessReportQueue, assetLock);

            BuildDebugWidget(teleportController, mvcManager, debugContainerBuilder);

            return new ParcelServiceContainer
            {
                RetrieveSceneFromFixedRealm = new RetrieveSceneFromFixedRealm(),
                RetrieveSceneFromVolatileWorld = new RetrieveSceneFromVolatileWorld(realmData),
                TeleportController = teleportController,
            };
        }

        private static void BuildDebugWidget(ITeleportController teleportController, IMVCManager mvcManager, IDebugContainerBuilder debugContainerBuilder)
        {
            var binding = new PersistentElementBinding<Vector2Int>(PersistentSetting.CreateVector2Int("teleportCoordinates"));

            UniTask ShowLoadingScreenAsync(AsyncLoadProcessReport loadReport) =>
                mvcManager.ShowAsync(
                    SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport))
                );

            debugContainerBuilder
               .TryAddWidget("Teleport")
              ?.AddControl(new DebugVector2IntFieldDef(binding), null)
               .AddControl(
                    new DebugButtonDef("To Parcel", () =>
                        {
                            AsyncLoadProcessReport loadReport = CreateReport();

                            UniTask.WhenAll(
                                        ShowLoadingScreenAsync(loadReport),
                                        teleportController.TeleportToParcelAsync(binding.Value, loadReport, CancellationToken.None)
                                    )
                                   .Forget();
                        }
                    ),
                    new DebugButtonDef("To Spawn Point", () =>
                        {
                            AsyncLoadProcessReport loadReport = CreateReport();

                            UniTask.WhenAll(
                                        ShowLoadingScreenAsync(loadReport),
                                        teleportController.TeleportToSceneSpawnPointAsync(binding.Value, loadReport, CancellationToken.None))
                                   .Forget();
                        }
                    )
                );

            static AsyncLoadProcessReport CreateReport() =>
                AsyncLoadProcessReport.Create(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
        }
    }
}
