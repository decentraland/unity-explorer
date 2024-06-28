using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    public interface IWebSocketApi : IDisposable
    {
        /// <summary>
        ///     The range of states of the JS WebSocket is narrower than on C# side
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum JSWebSocketState
        {
            CONNECTING = 0,
            OPEN = 1,
            CLOSING = 2,
            CLOSED = 3,
        }

        int CreateWebSocket(string url);

        UniTask ConnectAsync(int websocketId, string url, CancellationToken ct);

        UniTask SendBinaryAsync(int websocketId, IArrayBuffer data, CancellationToken ct);

        UniTask SendTextAsync(int websocketId, string data, CancellationToken ct);

        UniTask CloseAsync(int websocketId, CancellationToken ct);

        UniTask<ReceiveResponse> ReceiveAsync(int websocketId, CancellationToken ct);

        JSWebSocketState GetState(int webSocketId);

        /// <summary>
        ///     Response of the single Receive call
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public struct ReceiveResponse
        {
            /// <summary>
            ///     Either "Binary" or "Text"
            /// </summary>
            public string type;
            public ITypedArray<byte> data;
        }
    }
}
