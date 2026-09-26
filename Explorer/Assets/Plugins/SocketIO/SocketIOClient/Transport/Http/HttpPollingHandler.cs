using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using SocketIOClient.Extensions;

namespace SocketIOClient.Transport.Http
{
    public abstract class HttpPollingHandler : IHttpPollingHandler
    {
        protected HttpPollingHandler(IHttpClient adapter)
        {
            HttpClient = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        protected IHttpClient HttpClient { get; }
        public Func<string, UniTask> OnTextReceived { get; set; }
        public Action<byte[]> OnBytesReceived { get; set; }

        public void AddHeader(string key, string val)
        {
            HttpClient.AddHeader(key, val);
        }

        public void SetProxy(IWebProxy proxy)
        {
            HttpClient.SetProxy(proxy);
        }

        protected static string AppendRandom(string uri) =>
            uri + "&t=" + DateTimeOffset.Now.ToUnixTimeSeconds();

        public async UniTask GetAsync(string uri, CancellationToken cancellationToken)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, AppendRandom(uri));
            HttpResponseMessage resMsg = await HttpClient.SendAsync(req, cancellationToken);

            if (!resMsg.IsSuccessStatusCode) { throw new HttpRequestException($"Response status code does not indicate success: {resMsg.StatusCode}"); }

            await ProduceMessageAsync(resMsg);
        }

        public async UniTask SendAsync(HttpRequestMessage req, CancellationToken cancellationToken)
        {
            HttpResponseMessage resMsg = await HttpClient.SendAsync(req, cancellationToken);

            if (!resMsg.IsSuccessStatusCode) { throw new HttpRequestException($"Response status code does not indicate success: {resMsg.StatusCode}"); }

            await ProduceMessageAsync(resMsg);
        }

        public virtual async UniTask PostAsync(string uri, string content, CancellationToken cancellationToken)
        {
            var httpContent = new StringContent(content);
            HttpResponseMessage resMsg = await HttpClient.PostAsync(AppendRandom(uri), httpContent, cancellationToken);
            await ProduceMessageAsync(resMsg);
        }

        public abstract UniTask PostAsync(string uri, IEnumerable<byte[]> bytes, CancellationToken cancellationToken);

        private async UniTask ProduceMessageAsync(HttpResponseMessage resMsg)
        {
            if (resMsg.Content.Headers.ContentType.MediaType == "application/octet-stream")
            {
                byte[] bytes = await resMsg.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                await ProduceBytes(bytes);
            }
            else
            {
                string text = await resMsg.Content.ReadAsStringAsync().ConfigureAwait(false);
                await ProduceText(text);
            }
        }

        protected abstract UniTask ProduceText(string text);

        protected void OnBytes(byte[] bytes)
        {
            OnBytesReceived.TryInvoke(bytes);
        }

        private async UniTask ProduceBytes(byte[] bytes)
        {
            var i = 0;

            while (bytes.Length > i + 4)
            {
                byte type = bytes[i];
                var builder = new StringBuilder();
                i++;

                while (bytes[i] != byte.MaxValue)
                {
                    builder.Append(bytes[i]);
                    i++;
                }

                i++;
                var length = int.Parse(builder.ToString());

                if (type == 0)
                {
                    var buffer = new byte[length];
                    Buffer.BlockCopy(bytes, i, buffer, 0, buffer.Length);
                    await OnTextReceived.TryInvokeAsync(Encoding.UTF8.GetString(buffer));
                }
                else if (type == 1)
                {
                    var buffer = new byte[length - 1];
                    Buffer.BlockCopy(bytes, i + 1, buffer, 0, buffer.Length);
                    OnBytes(buffer);
                }

                i += length;
            }
        }

        public static IHttpPollingHandler CreateHandler(EngineIO eio, IHttpClient adapter)
        {
            if (eio == EngineIO.V3)
                return new Eio3HttpPollingHandler(adapter);

            return new Eio4HttpPollingHandler(adapter);
        }
    }
}
