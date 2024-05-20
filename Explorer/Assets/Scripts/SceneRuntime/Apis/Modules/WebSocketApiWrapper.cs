using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace SceneRuntime.Apis.Modules
{
    public class WebSocketApiWrapper : JsApiWrapperBase<IWebSocketApi>
    {
        private readonly CancellationTokenSource cancellationTokenSource;

        public WebSocketApiWrapper(IWebSocketApi api) : base(api)
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        protected override void DisposeInternal()
        {
            cancellationTokenSource.SafeCancelAndDispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public int Create(string url) =>
            api.CreateWebSocket(url);

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ConnectAsync(int websocketId, string url)
        {
            try { return api.ConnectAsync(websocketId, url, cancellationTokenSource.Token).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int websocketId, object data)
        {
            try { return api.SendAsync(websocketId, data, cancellationTokenSource.Token).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ReceiveAsync(int websocketId)
        {
            try { return api.ReceiveAsync(websocketId, cancellationTokenSource.Token).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object CloseAsync(int websocketId)
        {
            try { return api.CloseAsync(websocketId, cancellationTokenSource.Token).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
