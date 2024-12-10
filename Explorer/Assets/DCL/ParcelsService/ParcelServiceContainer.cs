using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.SceneLoadingScreens;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities.Extensions;
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
            LoadingScreenTimeout loadingScreenTimeout,
            ILoadingScreen loadingScreen,
            SceneAssetLock assetLock)
        {
            var teleportController = new TeleportController(sceneReadinessReportQueue, assetLock);

            BuildDebugWidget(teleportController, debugContainerBuilder, loadingScreen, loadingScreenTimeout);

            return new ParcelServiceContainer
            {
                RetrieveSceneFromFixedRealm = new RetrieveSceneFromFixedRealm(),
                RetrieveSceneFromVolatileWorld = new RetrieveSceneFromVolatileWorld(realmData),
                TeleportController = teleportController,
            };
        }

        private static void BuildDebugWidget(ITeleportController teleportController, IDebugContainerBuilder debugContainerBuilder, ILoadingScreen loadingScreen, LoadingScreenTimeout loadingScreenTimeout)
        {
            var binding = new PersistentElementBinding<Vector2Int>(PersistentSetting.CreateVector2Int("teleportCoordinates"));

            var timeout = new ElementBinding<float>((float)loadingScreenTimeout.Value.TotalSeconds,
                evt => loadingScreenTimeout.Value = TimeSpan.FromSeconds(Mathf.Max(evt.newValue, 0)));

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
