using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Decentraland.Sdk.Development;
using ECS.SceneLifeCycle.Systems;
using Google.Protobuf;
using System;
using System.Net.WebSockets;
using System.Threading;

namespace ECS.SceneLifeCycle.LocalSceneDevelopment
{
    [LogCategory(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT)]
    public class LocalSceneDevelopmentController
    {
        private readonly ReloadSceneController reloadController;

        private bool initialized = false;
        private ClientWebSocket webSocket;

        public LocalSceneDevelopmentController(ReloadSceneController reloadController)
        {
            this.reloadController = reloadController;
        }

        public void Initialize(string localSceneServer)
        {
            if (initialized) return;

            initialized = true;

            ConnectToServerAsync(
                localSceneServer.Contains("https") ? localSceneServer.Replace("https", "wss") : localSceneServer.Replace("http", "ws"),
                new WsSceneMessage(),
                new byte[1024]
                ).Forget();
        }

        private async UniTaskVoid ConnectToServerAsync(string localSceneWebsocketServer, WsSceneMessage wsSceneMessage, byte[] receiveBuffer)
        {
            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Trying to connect to: {localSceneWebsocketServer}");

            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(localSceneWebsocketServer), CancellationToken.None).AsUniTask();

            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection state: {webSocket.State}");

            while (webSocket.State == WebSocketState.Open)
            {
                var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Binary)
                {
                    byte[] finalBuffer = new byte[receiveResult.Count];
                    for (int i = 0; i < receiveResult.Count; i++)
                    {
                        finalBuffer[i] = receiveBuffer[i];
                    }

                    wsSceneMessage.MergeFrom(new CodedInputStream(finalBuffer));
                    ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket scene message received: {wsSceneMessage.MessageCase}");

                    // TODO: Discriminate 'wsSceneMessage.MessageCase == WsSceneMessage.MessageOneofCase.UpdateModel' to only update GLTF models...

                    await reloadController.TryReloadSceneAsync();
                }
                /*else // the old string message is sent more than once on the same scene update...
                {
                    var receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count);
                    ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection received message: {receivedMessage}");
                }*/
            }

            ReportHub.Log(ReportCategory.SDK_LOCAL_SCENE_DEVELOPMENT, $"Websocket connection closed.");
        }

        public void Dispose()
        {
            try { webSocket?.Dispose(); }
            catch (ObjectDisposedException) { }
        }
    }
}
