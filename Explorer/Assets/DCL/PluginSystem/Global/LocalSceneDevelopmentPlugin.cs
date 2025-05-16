using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.LocalSceneDevelopment;
using Global.Dynamic.RealmUrl;
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
        private readonly IRealmUrls realmUrls;
        private LocalSceneDevelopmentController? localSceneDevelopmentController;
        private CancellationTokenSource? lifeCycleCancellationTokenSource;

        public LocalSceneDevelopmentPlugin(IReloadScene reloadSceneController,
            IRealmUrls realmUrls)
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
            string realm = await realmUrls.LocalSceneDevelopmentRealmAsync(ct) ?? string.Empty;

            localSceneDevelopmentController = new LocalSceneDevelopmentController(reloadSceneController,
                playerEntity,
                world);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await localSceneDevelopmentController.ConnectToServerAsync(
                        realm.Contains("https") ? realm.Replace("https", "wss") : realm.Replace("http", "ws"),
                        ct);
                }
                catch (WebSocketException) { await UniTask.Delay(RETRY_DELAY_MS, cancellationToken: ct); }
                catch (SocketException) { await UniTask.Delay(RETRY_DELAY_MS, cancellationToken: ct); }
            }
        }
    }
}
