using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SocketIOClient.Transport.Http
{
    public interface IHttpClient : IDisposable
    {
        void AddHeader(string name, string value);

        void SetProxy(IWebProxy proxy);

        UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);

        UniTask<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken);

        UniTask<string> GetStringAsync(Uri requestUri);
    }
}
