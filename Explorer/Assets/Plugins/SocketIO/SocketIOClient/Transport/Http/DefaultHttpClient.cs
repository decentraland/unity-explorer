using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace SocketIOClient.Transport.Http
{
#if !UNITY_WEBGL
    public class DefaultHttpClient : IHttpClient
    {
        private readonly HttpClientHandler _handler;
        private readonly HttpClient _httpClient;

        public DefaultHttpClient()
        {
            _handler = new HttpClientHandler();
            _httpClient = new HttpClient(_handler);
        }

        private static readonly HashSet<string> allowedHeaders = new()
        {
            "user-agent",
        };

        public void AddHeader(string name, string value)
        {
            if (_httpClient.DefaultRequestHeaders.Contains(name)) { _httpClient.DefaultRequestHeaders.Remove(name); }

            if (allowedHeaders.Contains(name.ToLower())) { _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(name, value); }
            else { _httpClient.DefaultRequestHeaders.Add(name, value); }
        }

        public IEnumerable<string> GetHeaderValues(string name) =>
            _httpClient.DefaultRequestHeaders.GetValues(name);

        public void SetProxy(IWebProxy proxy)
        {
            _handler.Proxy = proxy;
        }

        public UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _httpClient.SendAsync(request, cancellationToken);

        public UniTask<HttpResponseMessage> PostAsync(string requestUri,
            HttpContent content,
            CancellationToken cancellationToken) =>
            _httpClient.PostAsync(requestUri, content, cancellationToken);

        public UniTask<string> GetStringAsync(Uri requestUri) =>
            _httpClient.GetStringAsync(requestUri);

        public void Dispose()
        {
            _httpClient.Dispose();
            _handler.Dispose();
        }
    }
#else
    // TODO Implement if requierd to support HTTP Transport
    public class DefaultHttpClient : IHttpClient
    {
        public void AddHeader(string name, string value)
        {
            throw new Exception("AddHeader is not supported");
        }

        public IEnumerable<string> GetHeaderValues(string name)
        {
            throw new Exception("GetHeaderValues is not supported");
        }

        public void SetProxy(IWebProxy proxy)
        {
            throw new Exception("SetProxy is not supported");
        }

        public UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new Exception("SetProxy is not supported");
        }

        public UniTask<HttpResponseMessage> PostAsync(string requestUri,
            HttpContent content,
            CancellationToken cancellationToken)
        {
            throw new Exception("SetProxy is not supported");
        }

        public UniTask<string> GetStringAsync(Uri requestUri)
        {
            throw new Exception("SetProxy is not supported");
        }

        public void Dispose()
        {
        }
    }
#endif
}
