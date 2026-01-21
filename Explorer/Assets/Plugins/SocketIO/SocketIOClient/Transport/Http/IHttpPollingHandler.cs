using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SocketIOClient.Transport.Http
{
    public interface IHttpPollingHandler
    {
        Func<string, UniTask> OnTextReceived { get; set; }
        Action<byte[]> OnBytesReceived { get; set; }

        UniTask GetAsync(string uri, CancellationToken cancellationToken);

        UniTask SendAsync(HttpRequestMessage req, CancellationToken cancellationToken);

        UniTask PostAsync(string uri, string content, CancellationToken cancellationToken);

        UniTask PostAsync(string uri, IEnumerable<byte[]> bytes, CancellationToken cancellationToken);

        void AddHeader(string key, string val);

        void SetProxy(IWebProxy proxy);
    }
}
