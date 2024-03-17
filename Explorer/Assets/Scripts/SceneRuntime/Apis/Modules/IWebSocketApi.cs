using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    public interface IWebSocketApi : IDisposable
    {
        public void CreateWebSocket(string url);

        public UniTask ConnectAsync(CancellationToken ct);
        public UniTask SendAsync(string data, CancellationToken ct);
        public UniTask CloseAsync(CancellationToken ct);
        public UniTask<string> ReceiveAsync(CancellationToken ct);

    }
}
