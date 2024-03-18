using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    public interface IWebSocketApi : IDisposable
    {
        public void CreateWebSocket(string url);

        public UniTask ConnectAsync(string url, CancellationToken ct);
        public UniTask SendAsync(object data, CancellationToken ct);
        public UniTask CloseAsync(CancellationToken ct);
        public UniTask<object> ReceiveAsync(CancellationToken ct);

    }
}
