using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utility.Networking;

namespace Utility.Tests
{
    /// <summary>
    ///     The JS WebSocket polyfill allows close() while the socket is still CONNECTING (WHATWG:
    ///     close() during the handshake fails the connection). mono's ClientWebSocket reports itself
    ///     Connected before the HTTP upgrade completes while its close path derefs an inner socket
    ///     that only exists after a successful upgrade, so CloseAsync on a pending (or never started)
    ///     connection must abort instead of entering the close handshake.
    /// </summary>
    [TestFixture]
    public class DCLWebSocketCloseAsyncShould
    {
        private const int TIMEOUT_MS = 10_000;

        [Test]
        public async Task AbortInsteadOfThrowingWhenClosedDuringHandshake()
        {
            // Arrange
            // Accepts the TCP connection but never answers the HTTP upgrade, so the client
            // stays parked mid-handshake with its inner managed socket unassigned.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task<Socket> serverAccept = listener.AcceptSocketAsync();
            Task acceptObserved = serverAccept.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

            var webSocket = new DCLWebSocket();

            try
            {
                Task connectTask = webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None).AsTask();

                // The parked connect surfaces its abort on a background continuation; observe it there
                // so no unobserved fault leaks past this test.
                Task connectObserved = connectTask.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.Connecting),
                    "Precondition: the socket must be observed mid-handshake (the JS readyState CONNECTING window)");

                // Act
                Exception? thrown = await CaptureAsync(() => webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None));

                // Assert
                Assert.That(thrown, Is.Null,
                    $"CloseAsync during the connect handshake must fail the connection, not throw, but threw {thrown?.GetType()}: {thrown?.Message}");

                Task completed = await Task.WhenAny(connectObserved, Task.Delay(TIMEOUT_MS));
                Assert.That(completed, Is.SameAs(connectObserved), "The aborted connect must complete instead of hanging");
            }
            finally
            {
                webSocket.Dispose();
                listener.Stop();
                await Task.WhenAny(acceptObserved, Task.Delay(TIMEOUT_MS));

                if (serverAccept.Status == TaskStatus.RanToCompletion)
                    serverAccept.Result.Dispose();
            }
        }

        [Test]
        public async Task UnparkAPendingConnectWhenAborted()
        {
            // Arrange
            // Accepts the TCP connection but never answers the HTTP upgrade, so the client
            // stays parked mid-handshake; the public Abort() must fail that pending connection
            // the same way the CloseAsync gate does.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task<Socket> serverAccept = listener.AcceptSocketAsync();
            Task acceptObserved = serverAccept.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

            var webSocket = new DCLWebSocket();

            try
            {
                Task connectTask = webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None).AsTask();

                // The parked connect surfaces its abort on a background continuation; observe it there
                // so no unobserved fault leaks past this test.
                Task connectObserved = connectTask.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.Connecting),
                    "Precondition: the socket must be observed mid-handshake");

                // Act
                webSocket.Abort();

                // Assert
                Task completed = await Task.WhenAny(connectObserved, Task.Delay(TIMEOUT_MS));
                Assert.That(completed, Is.SameAs(connectObserved), "Abort() must complete a parked connect instead of leaving it hanging");
            }
            finally
            {
                webSocket.Dispose();
                listener.Stop();
                await Task.WhenAny(acceptObserved, Task.Delay(TIMEOUT_MS));

                if (serverAccept.Status == TaskStatus.RanToCompletion)
                    serverAccept.Result.Dispose();
            }
        }

        [Test]
        public async Task ConnectFromAThreadWithoutSynchronizationContext()
        {
            // Arrange
            // The production caller is the ClearScript/V8 script-invoke thread, which carries no
            // TaskScheduler-compatible SynchronizationContext, so the whole connect/close path must
            // work with SynchronizationContext.Current == null.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task<Socket> serverAccept = listener.AcceptSocketAsync();
            Task acceptObserved = serverAccept.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

            var webSocket = new DCLWebSocket();

            try
            {
                Task? connectTask = null;
                Exception? synchronousThrow = null;

                // Act
                await Task.Run(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(null);

                    try { connectTask = webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None).AsTask(); }
                    catch (Exception e) { synchronousThrow = e; }
                });

                // Assert
                Assert.That(synchronousThrow, Is.Null,
                    $"ConnectAsync must not throw on a thread without a SynchronizationContext, but threw {synchronousThrow?.GetType()}: {synchronousThrow?.Message}");

                Task connectObserved = connectTask!.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

                Assert.That(connectTask.IsFaulted, Is.False,
                    $"ConnectAsync must not fault on a thread without a SynchronizationContext, but faulted with {connectTask.Exception?.GetBaseException().GetType()}: {connectTask.Exception?.GetBaseException().Message}");

                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.Connecting),
                    "Precondition: the socket must be observed mid-handshake");

                // Act
                Exception? thrown = await CaptureAsync(() => webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None));

                // Assert
                Assert.That(thrown, Is.Null,
                    $"CloseAsync after an off-context connect must fail the connection, not throw, but threw {thrown?.GetType()}: {thrown?.Message}");

                Task completed = await Task.WhenAny(connectObserved, Task.Delay(TIMEOUT_MS));
                Assert.That(completed, Is.SameAs(connectObserved), "The aborted connect must complete instead of hanging");
            }
            finally
            {
                webSocket.Dispose();
                listener.Stop();
                await Task.WhenAny(acceptObserved, Task.Delay(TIMEOUT_MS));

                if (serverAccept.Status == TaskStatus.RanToCompletion)
                    serverAccept.Result.Dispose();
            }
        }

        [Test]
        public async Task CompleteCleanlyOnANeverConnectedSocket()
        {
            // Arrange
            var webSocket = new DCLWebSocket();

            try
            {
                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.None),
                    "Precondition: no connect attempt was started");

                // Act
                Exception? thrown = await CaptureAsync(() => webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None));

                // Assert
                Assert.That(thrown, Is.Null,
                    $"CloseAsync on a never-connected socket must be a no-op abort, but threw {thrown?.GetType()}: {thrown?.Message}");
            }
            finally
            {
                webSocket.Dispose();
            }
        }

        [Test]
        public async Task StillPerformTheCloseHandshakeOnAnOpenSocket()
        {
            // Arrange
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task serverTask = ServeUpgradeThenEchoCloseAsync(listener);

            // Observed on a background continuation so no unobserved fault leaks if the test fails early
            _ = serverTask.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

            var webSocket = new DCLWebSocket();

            try
            {
                await AwaitWithTimeoutAsync(webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None).AsTask(), "connect");

                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.Open), "Precondition: the upgrade completed");

                // Act
                Exception? thrown = await CaptureAsync(() => webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None));

                // Assert
                Assert.That(thrown, Is.Null,
                    $"CloseAsync on an open socket must run the close handshake, but threw {thrown?.GetType()}: {thrown?.Message}");

                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.Closed), "The close handshake must complete both ways");

                await AwaitWithTimeoutAsync(serverTask, "server close echo");
            }
            finally
            {
                webSocket.Dispose();
                listener.Stop();
            }
        }

        [Test]
        public async Task ResumeOnTheCallerSynchronizationContextAfterConnecting()
        {
            // Arrange
            // The social/comms RPC transports start their receive loop in the connect continuation,
            // and that loop hands every incoming message to main-thread-only UI handlers. A connect
            // issued on a SynchronizationContext (the Unity main thread in production) must therefore
            // resume back on it, not on the socket's completion thread, or the loop and its handlers
            // run off the main thread.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task serverTask = ServeUpgradeThenEchoCloseAsync(listener);
            _ = serverTask.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);

            using var context = new SingleThreadSynchronizationContext();
            var webSocket = new DCLWebSocket();

            var resumeThreadId = 0;
            Exception? failure = null;

            try
            {
                var done = new TaskCompletionSource<bool>();

                // Drive the connect from the dedicated context thread and record where it resumes.
                context.Post(_ =>
                {
                    ConnectAndCaptureAsync().Forget();
                    return;

                    async UniTaskVoid ConnectAndCaptureAsync()
                    {
                        try
                        {
                            await webSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None);
                            resumeThreadId = Thread.CurrentThread.ManagedThreadId;
                        }
                        catch (Exception e) { failure = e; }
                        finally { done.TrySetResult(true); }
                    }
                }, null);

                await AwaitWithTimeoutAsync(done.Task, "connect on a SynchronizationContext");

                // Assert
                Assert.That(failure, Is.Null, $"ConnectAsync on a SynchronizationContext must not fault, but threw {failure?.GetType()}: {failure?.Message}");
                Assert.That(webSocket.State, Is.EqualTo(WebSocketState.Open), "Precondition: the upgrade completed");
                Assert.That(resumeThreadId, Is.EqualTo(context.ThreadId),
                    "ConnectAsync must resume on the caller's SynchronizationContext so the receive loop it starts stays on that thread");
            }
            finally
            {
                webSocket.Dispose();
                listener.Stop();
                await Task.WhenAny(serverTask, Task.Delay(TIMEOUT_MS));
            }
        }

        private static async Task<Exception?> CaptureAsync(Func<UniTask> operation)
        {
            try { await operation(); }
            catch (Exception e) { return e; }

            return null;
        }

        private static async Task AwaitWithTimeoutAsync(Task task, string what)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(TIMEOUT_MS));
            Assert.That(completed, Is.SameAs(task), $"{what} did not complete within {TIMEOUT_MS}ms");
            await task;
        }

        private static async Task ServeUpgradeThenEchoCloseAsync(TcpListener listener)
        {
            using Socket socket = await listener.AcceptSocketAsync();
            using var stream = new NetworkStream(socket, ownsSocket: false);

            string request = await ReadHeadersAsync(stream);
            var key = string.Empty;

            foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                    key = line.Substring("Sec-WebSocket-Key:".Length).Trim();

            string acceptKey;

            using (var sha1 = SHA1.Create())
                acceptKey = Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

            byte[] response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: {acceptKey}\r\n\r\n");

            await stream.WriteAsync(response, 0, response.Length);

            // Client close frame: FIN+opcode 0x8, masked payload. Echo the unmasked payload back
            // so the client-side close handshake can complete.
            var header = new byte[2];
            await ReadExactAsync(stream, header, header.Length);
            int payloadLength = header[1] & 0x7F;
            var maskAndPayload = new byte[4 + payloadLength];
            await ReadExactAsync(stream, maskAndPayload, maskAndPayload.Length);

            var closeReply = new byte[2 + payloadLength];
            closeReply[0] = 0x88;
            closeReply[1] = (byte)payloadLength;

            for (var i = 0; i < payloadLength; i++)
                closeReply[2 + i] = (byte)(maskAndPayload[4 + i] ^ maskAndPayload[i % 4]);

            await stream.WriteAsync(closeReply, 0, closeReply.Length);
        }

        private static async Task<string> ReadHeadersAsync(NetworkStream stream)
        {
            var builder = new StringBuilder();
            var single = new byte[1];

            while (!EndsWithHeaderTerminator(builder))
            {
                int read = await stream.ReadAsync(single, 0, 1);

                if (read == 0)
                    throw new IOException("Connection closed before the upgrade request completed");

                builder.Append((char)single[0]);
            }

            return builder.ToString();
        }

        private static bool EndsWithHeaderTerminator(StringBuilder builder) =>
            builder.Length >= 4
            && builder[builder.Length - 4] == '\r' && builder[builder.Length - 3] == '\n'
            && builder[builder.Length - 2] == '\r' && builder[builder.Length - 1] == '\n';

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
        {
            var offset = 0;

            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset);

                if (read == 0)
                    throw new IOException("Connection closed mid-frame");

                offset += read;
            }
        }

        /// <summary>
        ///     A pumped SynchronizationContext backed by one dedicated thread, standing in for Unity's
        ///     single-threaded main-thread context: continuations posted to it run on that one thread,
        ///     so a test can assert an await resumed on the context it was issued from.
        /// </summary>
        private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<(SendOrPostCallback callback, object? state)> queue = new ();
            private readonly Thread thread;

            public int ThreadId => thread.ManagedThreadId;

            public SingleThreadSynchronizationContext()
            {
                thread = new Thread(Pump) { IsBackground = true, Name = nameof(SingleThreadSynchronizationContext) };
                thread.Start();
            }

            public override void Post(SendOrPostCallback d, object? state) =>
                queue.Add((d, state));

            public override void Send(SendOrPostCallback d, object? state) =>
                throw new NotSupportedException();

            public void Dispose() =>
                queue.CompleteAdding();

            private void Pump()
            {
                SetSynchronizationContext(this);

                foreach ((SendOrPostCallback callback, object? state) in queue.GetConsumingEnumerable())
                    callback(state);
            }
        }
    }
}
