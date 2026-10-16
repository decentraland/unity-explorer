using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NSubstitute;
using NUnit.Framework;
using Pulse.Transport;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class PulseMultiplayerServiceShould
    {
        private ITransport transport = null!;
        private IDecentralandUrlsSource urlsSource = null!;
        private MessagePipe pipe = null!;
        private PulseMultiplayerService service = null!;
        private CancellationTokenSource cts = null!;

        [SetUp]
        public void SetUp()
        {
            transport = Substitute.For<ITransport>();
            transport.State.Returns(ITransport.TransportState.None);

            urlsSource = Substitute.For<IDecentralandUrlsSource>();

            pipe = new MessagePipe();
            service = new PulseMultiplayerService(transport, pipe, urlsSource);
            cts = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            cts.Dispose();
        }

        [Test]
        public void ReturnFalseWhenUnreachableWithinMaxAttempts()
        {
            // Arrange
            transport
               .ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns(_ => throw new TimeoutException());

            // Act
            bool connected = service.ConnectAsync(cts.Token, maxAttempts: 1).GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(connected);
            Assert.IsFalse(service.IsAuthenticated);
            transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void NotAttemptConnectionWhenAlreadyConnected()
        {
            // Arrange
            transport.State.Returns(ITransport.TransportState.Connected);

            // Act
            bool connected = service.ConnectAsync(cts.Token, maxAttempts: 1).GetAwaiter().GetResult();

            // Assert
            Assert.IsTrue(connected);
            transport.DidNotReceive().ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ReturnFalseWhenServerDisconnectsDuringHandshake()
        {
            // Arrange
            var disconnectHandlerCalled = false;

            service.RegisterDisconnectHandler(_ =>
            {
                disconnectHandlerCalled = true;
                return (true, TimeSpan.Zero);
            });

            service.RegisterHandshakeHandler(async (handshakeReceived, _) =>
            {
                // The server drops the connection instead of answering the handshake
                pipe.OnDisconnected(DisconnectReason.GRACEFUL);
                await handshakeReceived.Task;
            });

            // Act
            // The routing loop faults the handshake completion from the thread pool, so ConnectAsync
            // completes asynchronously — await it instead of GetResult, which cannot block on a UniTask.
            bool connected = await service.ConnectAsync(cts.Token, maxAttempts: 1);

            // Assert
            Assert.IsFalse(connected);
            Assert.IsFalse(service.IsAuthenticated);
            Assert.IsFalse(disconnectHandlerCalled, "Mid-handshake disconnects must not trigger the reconnection path");
        }

        [Test]
        public async Task RetryWithBackoffWhenServerDisconnectsDuringHandshake()
        {
            // Arrange
            service.RegisterHandshakeHandler(async (handshakeReceived, _) =>
            {
                pipe.OnDisconnected(DisconnectReason.GRACEFUL);
                await handshakeReceived.Task;
            });

            // Act
            bool connected = await service.ConnectAsync(cts.Token, maxAttempts: 2);

            // Assert
            Assert.IsFalse(connected);
            Assert.IsFalse(service.IsAuthenticated);
            _ = transport.Received(2).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void NotRetryWhenHandshakeIsRejected()
        {
            // Arrange
            service.RegisterHandshakeHandler((_, _) => throw new PulseHandshakeDisconnectedException("Handshake rejected"));

            // Act
            bool connected = service.ConnectAsync(cts.Token, maxAttempts: 3).GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(connected);
            Assert.IsFalse(service.IsAuthenticated);
            transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task NotRetryWhenDisconnectReasonDuringHandshakeIsTerminal()
        {
            // Arrange
            service.RegisterHandshakeHandler(async (handshakeReceived, _) =>
            {
                pipe.OnDisconnected(DisconnectReason.BANNED);
                await handshakeReceived.Task;
            });

            // Act
            bool connected = await service.ConnectAsync(cts.Token, maxAttempts: 3);

            // Assert
            Assert.IsFalse(connected);
            Assert.IsFalse(service.IsAuthenticated);
            _ = transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void RouteDisconnectToDisconnectHandlerAfterSuccessfulHandshake()
        {
            // Arrange
            var disconnectHandled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            service.RegisterDisconnectHandler(_ =>
            {
                disconnectHandled.TrySetResult(true);
                return (false, TimeSpan.Zero);
            });

            service.RegisterHandshakeHandler((handshakeReceived, _) =>
            {
                handshakeReceived.TrySetResult((true, null));
                return UniTask.CompletedTask;
            });

            // Act
            bool connected = service.ConnectAsync(cts.Token, maxAttempts: 1).GetAwaiter().GetResult();
            pipe.OnDisconnected(DisconnectReason.GRACEFUL);

            // Assert
            Assert.IsTrue(connected);
            Assert.IsTrue(service.IsAuthenticated);
            Assert.IsTrue(disconnectHandled.Task.Wait(TimeSpan.FromSeconds(5)), "Post-handshake disconnects must reach the disconnect handler");
        }

        [Test]
        public async Task NotReconnectAfterExplicitDisconnect()
        {
            // Arrange
            var serverDisconnectHandled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            service.RegisterDisconnectHandler(reason =>
            {
                if (reason == DisconnectReason.AUTH_TIMEOUT)
                    serverDisconnectHandled.TrySetResult(true);

                return (true, TimeSpan.FromMilliseconds(100));
            });

            RegisterSettlingHandshakeHandler();
            await service.ConnectAsync(cts.Token, maxAttempts: 1);

            // Act
            // The routing loop schedules the automatic reconnection after the handler's delay;
            // the explicit disconnect that follows ends the session before that delay elapses.
            pipe.OnDisconnected(DisconnectReason.AUTH_TIMEOUT);
            Task handled = await Task.WhenAny(serverDisconnectHandled.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(serverDisconnectHandled.Task, handled, "The server disconnect must reach the reconnection path first");
            await service.DisconnectAsync();
            await Task.Delay(300);

            // Assert
            Assert.IsFalse(service.IsAuthenticated);
            _ = transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task IgnoreDisconnectWithoutSession()
        {
            // Arrange
            var disconnectHandlerCalled = false;

            service.RegisterDisconnectHandler(_ =>
            {
                disconnectHandlerCalled = true;
                return (false, TimeSpan.Zero);
            });

            // Act
            await service.DisconnectAsync();

            // Assert
            Assert.IsFalse(disconnectHandlerCalled, "Disconnecting without a session must not reach the disconnect handler");
            _ = transport.DidNotReceive().DisconnectAsync(Arg.Any<DisconnectReason>());
        }

        [Test]
        public async Task ResetAuthenticationOnDisconnect()
        {
            // Arrange
            RegisterSettlingHandshakeHandler();
            bool connected = await service.ConnectAsync(cts.Token, maxAttempts: 1);
            Assert.IsTrue(connected);
            Assert.IsTrue(service.IsAuthenticated);

            // Act
            await service.DisconnectAsync();

            // Assert
            Assert.IsFalse(service.IsAuthenticated);
            _ = transport.Received(1).DisconnectAsync(DisconnectReason.GRACEFUL);
        }

        [Test]
        public async Task ShareInFlightAttemptBetweenConcurrentConnectCalls()
        {
            // Arrange
            var transportConnected = new UniTaskCompletionSource();

            transport
               .ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns(transportConnected.Task);

            RegisterSettlingHandshakeHandler();

            // Act
            // Both callers await while the transport connection is still in flight: the state stays None,
            // so only the in-flight attempt can stop the second call from starting another connection.
            UniTask<bool> first = service.ConnectAsync(cts.Token);
            UniTask<bool> second = service.ConnectAsync(cts.Token);
            UniTask<(bool, bool)> both = UniTask.WhenAll(first, second);
            transportConnected.TrySetResult();
            (bool firstConnected, bool secondConnected) = await both;

            // Assert
            Assert.IsTrue(firstConnected);
            Assert.IsTrue(secondConnected);
            Assert.IsTrue(service.IsAuthenticated);
            _ = transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task AllowConnectingAgainAfterDisconnect()
        {
            // Arrange
            RegisterSettlingHandshakeHandler();
            await service.ConnectAsync(cts.Token, maxAttempts: 1);
            await service.DisconnectAsync();

            // Act
            bool reconnected = await service.ConnectAsync(cts.Token, maxAttempts: 1);

            // Assert
            Assert.IsTrue(reconnected);
            Assert.IsTrue(service.IsAuthenticated);
            _ = transport.Received(2).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task WaitForTeardownBeforeConnectingAgain()
        {
            // Arrange
            RegisterSettlingHandshakeHandler();
            await service.ConnectAsync(cts.Token, maxAttempts: 1);

            var teardownFinished = new UniTaskCompletionSource();
            transport.DisconnectAsync(Arg.Any<DisconnectReason>()).Returns(teardownFinished.Task);
            transport.State.Returns(ITransport.TransportState.Connected);

            // Act
            UniTask disconnect = service.DisconnectAsync();
            UniTask<bool> reconnect = service.ConnectAsync(cts.Token, maxAttempts: 1);

            // The transport must not be connected again while its teardown is in progress
            _ = transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

            teardownFinished.TrySetResult();
            await disconnect;
            bool reconnected = await reconnect;

            // Assert
            Assert.IsTrue(reconnected);
            _ = transport.Received(2).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task WaitForTheCancelledAttemptBeforeConnectingAgain()
        {
            // Arrange
            RegisterSettlingHandshakeHandler();
            var cancelledConnectCleanedUp = new UniTaskCompletionSource();
            var connectCalls = 0;

            transport
               .ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
               .Returns(call => ++connectCalls == 1 ? ConnectUntilCancelledAsync(call.ArgAt<CancellationToken>(2)) : UniTask.CompletedTask);

            _ = service.ConnectAsync(cts.Token);

            // Act
            await service.DisconnectAsync();
            UniTask<bool> reconnect = service.ConnectAsync(cts.Token, maxAttempts: 1);

            // The cancelled attempt is still cleaning up its transport connection
            Assert.AreEqual(1, connectCalls);

            cancelledConnectCleanedUp.TrySetResult();
            bool reconnected = await reconnect;

            // Assert
            Assert.IsTrue(reconnected);
            Assert.AreEqual(2, connectCalls);

            async UniTask ConnectUntilCancelledAsync(CancellationToken ct)
            {
                try { await UniTask.Never(ct); }
                catch (OperationCanceledException)
                {
                    await cancelledConnectCleanedUp.Task;
                    throw;
                }
            }
        }

        [Test]
        public async Task DiscardEventsLeftByTheClosedConnection()
        {
            // Arrange
            RegisterSettlingHandshakeHandler();
            await service.ConnectAsync(cts.Token, maxAttempts: 1);

            // The transport queues a disconnect event while the session is being torn down
            transport.DisconnectAsync(Arg.Any<DisconnectReason>()).Returns(_ =>
            {
                pipe.OnDisconnected(DisconnectReason.AUTH_TIMEOUT);
                return UniTask.CompletedTask;
            });

            await service.DisconnectAsync();

            var staleDisconnectRouted = false;

            service.RegisterDisconnectHandler(reason =>
            {
                if (reason == DisconnectReason.AUTH_TIMEOUT)
                    staleDisconnectRouted = true;

                return (false, TimeSpan.Zero);
            });

            service.RegisterHandshakeHandler(async (handshakeReceived, _) =>
            {
                handshakeReceived.TrySetResult((true, null));
                await handshakeReceived.Task;
            });

            // Act
            bool reconnected = await service.ConnectAsync(cts.Token, maxAttempts: 1);
            await Task.Delay(200);

            // Assert
            Assert.IsTrue(reconnected);
            Assert.IsFalse(staleDisconnectRouted, "A disconnect of the closed connection must not reach the next session");
        }

        [Test]
        public async Task TolerateDisconnectTwice()
        {
            // Arrange
            RegisterSettlingHandshakeHandler();
            await service.ConnectAsync(cts.Token, maxAttempts: 1);
            await service.DisconnectAsync();

            // Act
            Assert.DoesNotThrowAsync(async () => await service.DisconnectAsync());

            // Assert
            Assert.IsFalse(service.IsAuthenticated);
            _ = transport.Received(1).DisconnectAsync(DisconnectReason.GRACEFUL);
        }

        /// <summary>
        ///     Settles the handshake synchronously so a connection attempt completes as soon as the transport connects.
        /// </summary>
        private void RegisterSettlingHandshakeHandler()
        {
            service.RegisterHandshakeHandler((handshakeReceived, _) =>
            {
                handshakeReceived.TrySetResult((true, null));
                return UniTask.CompletedTask;
            });
        }
    }
}
