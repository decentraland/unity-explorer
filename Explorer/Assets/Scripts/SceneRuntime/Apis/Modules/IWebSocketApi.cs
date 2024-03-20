using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    public interface IWebSocketApi : IDisposable
    {
        public int CreateWebSocket(string url);

        public UniTask ConnectAsync(int websocketId, string url, CancellationToken ct);

        public UniTask SendAsync(int websocketId, object data, CancellationToken ct);

        public UniTask CloseAsync(int websocketId, CancellationToken ct);

        public UniTask<object> ReceiveAsync(int websocketId, CancellationToken ct);
    }
}
