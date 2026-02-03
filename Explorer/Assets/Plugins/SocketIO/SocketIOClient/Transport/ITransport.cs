using System;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;
using SocketIOClient.Messages;

namespace SocketIOClient.Transport
{
    public interface ITransport : IDisposable
    {
        Action<IMessage> OnReceived { get; set; }
        Action<Exception> OnError { get; set; }
        string Namespace { get; set; }

        UniTask SendAsync(IMessage msg, CancellationToken cancellationToken);

        UniTask ConnectAsync(Uri uri, CancellationToken cancellationToken);

        UniTask DisconnectAsync(CancellationToken cancellationToken);
    }
}
