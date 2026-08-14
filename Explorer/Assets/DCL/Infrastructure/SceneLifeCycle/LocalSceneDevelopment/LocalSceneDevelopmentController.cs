using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.SkyBox;
using DCL.SkyBox.Components;
using Decentraland.Sdk.Development;
using ECS.StreamableLoading.AssetBundles;
using Google.Protobuf;
using System;
using System.Threading;
using Utility.Multithreading;
using Utility.Networking;

namespace ECS.SceneLifeCycle.LocalSceneDevelopment
{
    public class LocalSceneDevelopmentController
    {
        private const double RELOAD_SCENE_TIMEOUT_SECS = 5;

        private readonly ECSReloadScene reloadScene;
        private readonly Entity playerEntity;
        private readonly Entity skyboxEntity;
        private readonly World globalWorld;
        private DCLWebSocket? webSocket;

        public LocalSceneDevelopmentController(ECSReloadScene reloadScene,
            Entity playerEntity,
            Entity skyboxEntity,
            World globalWorld)
        {
            this.reloadScene = reloadScene;
            this.playerEntity = playerEntity;
            this.skyboxEntity = skyboxEntity;
            this.globalWorld = globalWorld;
        }

        public void Dispose()
        {
            try
            {
                webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                webSocket?.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        public async UniTask ConnectToServerAsync(string url, CancellationToken ct)
        {
            await ConnectToServerAsync(url, new WsSceneMessage(), new byte[1024], ct);
        }

        private async UniTask ConnectToServerAsync(string localSceneWebsocketServer,
            WsSceneMessage wsSceneMessage, byte[] receiveBuffer, CancellationToken ct)
        {
            await DCLTask.SwitchToThreadPool();

            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Trying to connect to: {localSceneWebsocketServer}");

            webSocket = new DCLWebSocket();
            await webSocket.ConnectAsync(new Uri(localSceneWebsocketServer), ct);

            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection state: {webSocket.State}");

            while (webSocket.State == WebSocketState.Open)
            {
                // every iteration starts on the thread pool
                await DCLTask.SwitchToThreadPool();

                WebSocketReceiveResult? receiveResult = await webSocket.ReceiveAsync(receiveBuffer, ct);

                if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    wsSceneMessage.MergeFrom(receiveBuffer.AsSpan(0, receiveResult.Count));
                    ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket scene message received: {wsSceneMessage.MessageCase}");

                    // An UpdateModel message names the single GLTF that changed; carry its src through so
                    // the reload can evict just that asset instead of draining every cache. The message's
                    // own hash is unusable — it is minted from the watcher-relative path while cache keys
                    // derive from the content-mapping hash, so the reload resolves the hash by src itself.
                    string sceneId;
                    string? changedModelSrc;

                    if (wsSceneMessage.MessageCase == WsSceneMessage.MessageOneofCase.UpdateModel)
                    {
                        sceneId = wsSceneMessage.UpdateModel.SceneId;
                        changedModelSrc = wsSceneMessage.UpdateModel.Src;
                    }
                    else
                    {
                        sceneId = wsSceneMessage.UpdateScene.SceneId;
                        changedModelSrc = null;
                    }

                    // Arm the abgen sidecar's reconversion mirror before the reload's manifest request
                    // lands; without --local-ab nothing consumes the signal and it stays inert.
                    AbgenConversionMetrics.INSTANCE.OnContentEdit(changedModelSrc);

                    // Switch to the main thread because `TryReloadSceneAsync` requires that
                    await UniTask.SwitchToMainThread(cancellationToken: ct);

                    try
                    {
                        // We need to freeze the character movement until the scene is reloaded
                        globalWorld.AddOrGet(playerEntity, new StopCharacterMotion());

                        // And pause the skybox update while loading to avoid transitions
                        globalWorld.AddOrGet(skyboxEntity, new PauseSkyboxTimeUpdate());

                        await reloadScene.TryReloadSceneAsync(ct, sceneId, changedModelSrc)
                                         .Timeout(TimeSpan.FromSeconds(RELOAD_SCENE_TIMEOUT_SECS));
                    }
                    catch (TimeoutException) { }
                    finally
                    {
                        globalWorld.Remove<StopCharacterMotion>(playerEntity);
                        globalWorld.Remove<PauseSkyboxTimeUpdate>(skyboxEntity);
                    }
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection closed.");
                }
            }
        }
    }
}
