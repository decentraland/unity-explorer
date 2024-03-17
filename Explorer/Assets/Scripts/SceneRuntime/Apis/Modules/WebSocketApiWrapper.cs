using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules
{
    public class WebSocketApiWrapper : IDisposable
    {
        private readonly IWebSocketApi api;
        private readonly CancellationTokenSource cancellationTokenSource;

        public WebSocketApiWrapper(IWebSocketApi api, ISceneExceptionsHandler exceptionsHandler)
        {
            this.api = api;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            api.Dispose();
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public void Create(string url) =>
            api.CreateWebSocket(url);

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ConnectAsync(string url)
        {
            try { return api.ConnectAsync(cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(string data)
        {
            try { return api.SendAsync(data, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ReceiveAsync()
        {
            try { return api.ReceiveAsync(cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object CloseAsync()
        {
            try { return api.CloseAsync(cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
