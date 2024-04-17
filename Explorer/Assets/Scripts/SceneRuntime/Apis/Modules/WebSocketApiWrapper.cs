using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace SceneRuntime.Apis.Modules
{
    public class WebSocketApiWrapper : IJsApiWrapper
    {
        private readonly IWebSocketApi api;
        private readonly CancellationTokenSource cancellationTokenSource;

        public WebSocketApiWrapper(IWebSocketApi api)
        {
            this.api = api;
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            api.Dispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public int Create(string url) =>
            api.CreateWebSocket(url);

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ConnectAsync(int websocketId, string url)
        {
            try { return api.ConnectAsync(websocketId, url, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int websocketId, object data)
        {
            try { return api.SendAsync(websocketId, data, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ReceiveAsync(int websocketId)
        {
            try { return api.ReceiveAsync(websocketId, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object CloseAsync(int websocketId)
        {
            try { return api.CloseAsync(websocketId, cancellationTokenSource.Token).AsTask().ToPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
