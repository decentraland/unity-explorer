using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules
{
    public class WebSocketApiWrapper : JsApiWrapper<IWebSocketApi>
    {
        private readonly IJavaScriptApiExceptionsHandler exceptionsHandler;
        private readonly bool isLocalSceneDevelopment;

        public WebSocketApiWrapper(IWebSocketApi api, IJavaScriptApiExceptionsHandler exceptionsHandler, CancellationTokenSource disposeCts, bool isLocalSceneDevelopment)
            : base(api, disposeCts)
        {
            this.exceptionsHandler = exceptionsHandler;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public int Create(string url) =>
            api.CreateWebSocket(url);

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public int GetState(int webSocketId) =>
            (int)api.GetState(webSocketId);

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ConnectAsync(int websocketId, string url)
        {
            try
            {
                // if we're in isLocalSceneDevelopment mode to allow connecting to unsafe websocket server to the client
                if (!isLocalSceneDevelopment && !url.ToLower().StartsWith("wss://"))
                    throw new Exception("Can't start an unsafe ws connection, please upgrade to wss. url=" + url);

                return api.ConnectAsync(websocketId, url, disposeCts.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(this);
            }
            catch (Exception e)
            {
                return Task.FromException(e).ToPromise();
            }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int webSocketId, string str)
        {
            try
            {
                return api.SendTextAsync(webSocketId, str, disposeCts.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise(this);
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int webSocketId, IArrayBuffer arrayBuffer)
        {
            try
            {
                return api.SendBinaryAsync(webSocketId, arrayBuffer, arrayBuffer.Size, disposeCts.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise(this);
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int webSocketId, ITypedArray<byte> typedArray)
        {
            try
            {
                // Apparently typedArray.Length != typedArray.ArrayBuffer.Length as the latter is preallocated to the bigger size (e.g. 8192)
                return api.SendBinaryAsync(webSocketId, typedArray.ArrayBuffer, typedArray.Length, disposeCts.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise(this);
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ReceiveAsync(int websocketId)
        {
            try
            {
                return api.ReceiveAsync(websocketId, disposeCts.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise(this);
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object CloseAsync(int websocketId)
        {
            try { return api.CloseAsync(websocketId, disposeCts.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(this); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
