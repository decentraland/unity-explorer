using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.LocalSceneDevelopment;
using Global.Dynamic.RealmUrl;
using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class LocalSceneDevelopmentPlugin : IDCLGlobalPlugin
    {
        private const int RETRY_DELAY_MS = 5000;

        private readonly IReloadScene reloadSceneController;
        private readonly RealmUrls realmUrls;
        private LocalSceneDevelopmentController? localSceneDevelopmentController;
        private CancellationTokenSource? lifeCycleCancellationTokenSource;

        public LocalSceneDevelopmentPlugin(IReloadScene reloadSceneController,
            RealmUrls realmUrls)
        {
            this.reloadSceneController = reloadSceneController;
            this.realmUrls = realmUrls;
        }

        public void Dispose()
        {
            localSceneDevelopmentController?.Dispose();
            lifeCycleCancellationTokenSource.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            lifeCycleCancellationTokenSource = lifeCycleCancellationTokenSource.SafeRestart();
            ConnectToServerAsync(arguments.PlayerEntity, builder.World, lifeCycleCancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid ConnectToServerAsync(Entity playerEntity, Arch.Core.World world, CancellationToken ct)
        {
            Uri? realm = await realmUrls.LocalSceneDevelopmentRealmAsync(ct);

            localSceneDevelopmentController = new LocalSceneDevelopmentController(reloadSceneController,
                playerEntity,
                world);

            if (realm == null) return;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    string realmString = realm.OriginalString;

                    await localSceneDevelopmentController.ConnectToServerAsync(
                        new Uri(realm.Scheme.Contains("https") ? realmString.Replace("https", "wss") : realmString.Replace("http", "ws")),
                        ct);
                }
                catch (OperationCanceledException) { break; }
                catch (WebSocketException e)
                {
                    // Only log to console as we don't want to flood sentry with this error
                    ReportHub.LogError(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT,
                        $"Error on local scene development web socket: {e}",
                        ReportHandler.DebugLog);

                    await UniTask.Delay(RETRY_DELAY_MS, cancellationToken: ct);
                }
                catch (SocketException e)
                {
                    // Only log to console as we don't want to flood sentry with this error
                    ReportHub.LogError(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT,
                        $"Error on local scene development web socket: {e}",
                        ReportHandler.DebugLog);

                    await UniTask.Delay(RETRY_DELAY_MS, cancellationToken: ct);
                }
            }
        }
    }
}
