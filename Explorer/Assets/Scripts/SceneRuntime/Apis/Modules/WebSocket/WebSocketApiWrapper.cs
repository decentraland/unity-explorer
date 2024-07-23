using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace SceneRuntime.Apis.Modules
{
    public class WebSocketApiWrapper : JsApiWrapperBase<IWebSocketApi>
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IJavaScriptApiExceptionsHandler exceptionsHandler;

        public WebSocketApiWrapper(IWebSocketApi api, IJavaScriptApiExceptionsHandler exceptionsHandler) : base(api)
        {
            this.exceptionsHandler = exceptionsHandler;
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
        public int GetState(int webSocketId) =>
            (int)api.GetState(webSocketId);

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ConnectAsync(int websocketId, string url)
        {
            try { return api.ConnectAsync(websocketId, url, cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int webSocketId, string str)
        {
            try
            {
                return api.SendTextAsync(webSocketId, str, cancellationTokenSource.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise();
            }
            catch (Exception e) { return Task.FromException(e).ToPromise();}
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int webSocketId, IArrayBuffer arrayBuffer)
        {
            try
            {
                return api.SendBinaryAsync(webSocketId, arrayBuffer, cancellationTokenSource.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise();
            }
            catch (Exception e) { return Task.FromException(e).ToPromise();}
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object SendAsync(int webSocketId, ITypedArray<byte> typedArray)
        {
            try
            {
                return api.SendBinaryAsync(webSocketId, typedArray.ArrayBuffer, cancellationTokenSource.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise();
            }
            catch (Exception e) { return Task.FromException(e).ToPromise();}
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object ReceiveAsync(int websocketId)
        {
            try
            {
                return api.ReceiveAsync(websocketId, cancellationTokenSource.Token)
                          .ReportAndRethrowException(exceptionsHandler)
                          .ToDisconnectedPromise();
            }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/webSocketApi.js")]
        public object CloseAsync(int websocketId)
        {
            try { return api.CloseAsync(websocketId, cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }
    }
}
