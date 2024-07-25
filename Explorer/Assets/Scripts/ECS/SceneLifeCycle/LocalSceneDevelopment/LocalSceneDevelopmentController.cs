using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Decentraland.Sdk.Development;
using ECS.SceneLifeCycle.Systems;
using Google.Protobuf;
using System;
using System.Net.WebSockets;
using System.Threading;
using Utility;

namespace ECS.SceneLifeCycle.LocalSceneDevelopment
{
    public class LocalSceneDevelopmentController
    {
        private readonly IReloadScene reloadScene;
        private readonly CancellationTokenSource connectToServerCancellationToken = new ();
        private ClientWebSocket? webSocket;

        public LocalSceneDevelopmentController(IReloadScene reloadScene, string localSceneServer)
        {
            this.reloadScene = reloadScene;

            ConnectToServerAsync(
                    localSceneServer.Contains("https") ? localSceneServer.Replace("https", "wss") : localSceneServer.Replace("http", "ws"),
                    new WsSceneMessage(),
                    new byte[1024],
                    connectToServerCancellationToken.Token
                )
               .Forget();
        }

        private async UniTaskVoid ConnectToServerAsync(string localSceneWebsocketServer, WsSceneMessage wsSceneMessage, byte[] receiveBuffer, CancellationToken ct)
        {
            await UniTask.SwitchToThreadPool();

            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Trying to connect to: {localSceneWebsocketServer}");

            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(localSceneWebsocketServer), ct);

            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection state: {webSocket.State}");

            while (webSocket.State == WebSocketState.Open)
            {
                // every iteration starts on the thread pool
                await UniTask.SwitchToThreadPool();

                WebSocketReceiveResult? receiveResult = await webSocket.ReceiveAsync(receiveBuffer, ct);

                if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    wsSceneMessage.MergeFrom(receiveBuffer.AsSpan(0, receiveResult.Count));
                    ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket scene message received: {wsSceneMessage.MessageCase}");

                    // TODO: Discriminate 'wsSceneMessage.MessageCase == WsSceneMessage.MessageOneofCase.UpdateModel' to only update GLTF models...

                    // Switch to the main thread because `TryReloadSceneAsync` requires that
                    await UniTask.SwitchToMainThread(cancellationToken: ct);
                    await reloadScene.TryReloadSceneAsync(ct);
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                    ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection closed.");
                }
            }
        }

        public void Dispose()
        {
            try
            {
                webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                webSocket?.Dispose();
            }
            catch (ObjectDisposedException) { }
            connectToServerCancellationToken.SafeCancelAndDispose();
        }
    }
}
