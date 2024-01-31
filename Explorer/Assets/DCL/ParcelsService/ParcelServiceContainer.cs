﻿using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.SceneLoadingScreens;
using ECS;
using ECS.SceneLifeCycle.Reporting;
using MVC;
using System;
using System.Threading;
using UnityEngine;

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
            MVCManager mvcManager)
        {
            var teleportController = new TeleportController(sceneReadinessReportQueue);

            BuildDebugWidget(teleportController, mvcManager, debugContainerBuilder);

            return new ParcelServiceContainer
            {
                RetrieveSceneFromFixedRealm = new RetrieveSceneFromFixedRealm(),
                RetrieveSceneFromVolatileWorld = new RetrieveSceneFromVolatileWorld(realmData),
                TeleportController = teleportController,
            };
        }

        private static void BuildDebugWidget(ITeleportController teleportController, MVCManager mvcManager, IDebugContainerBuilder debugContainerBuilder)
        {
            var binding = new ElementBinding<Vector2Int>(Vector2Int.zero);

            debugContainerBuilder.AddWidget("Teleport")
                                 .AddControl(new DebugVector2IntFieldDef(binding), null)
                                 .AddControl(
                                      new DebugButtonDef("To Parcel", () =>
                                      {
                                          var loadReport = AsyncLoadProcessReport.Create();

                                          UniTask.WhenAll(mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, TimeSpan.FromSeconds(30)))),
                                                      teleportController.TeleportToParcelAsync(binding.Value, loadReport, CancellationToken.None))
                                                 .Forget();
                                      }),
                                      new DebugButtonDef("To Spawn Point", () =>
                                      {
                                          var loadReport = AsyncLoadProcessReport.Create();

                                          UniTask.WhenAll(mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport, TimeSpan.FromSeconds(30)))),
                                                      teleportController.TeleportToSceneSpawnPointAsync(binding.Value, loadReport, CancellationToken.None))
                                                 .Forget();
                                      }));
        }
    }
}
