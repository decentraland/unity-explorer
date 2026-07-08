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
        private ITransport transport;
        private IDecentralandUrlsSource urlsSource;
        private MessagePipe pipe;
        private PulseMultiplayerService service;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            transport = Substitute.For<ITransport>();
            transport.State.Returns(ITransport.TransportState.NONE);

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
            transport.State.Returns(ITransport.TransportState.CONNECTED);

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
            transport.Received(2).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
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
            transport.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void RouteDisconnectToDisconnectHandlerAfterSuccessfulHandshake()
        {
            // Arrange
            using var disconnectHandled = new ManualResetEventSlim(false);

            service.RegisterDisconnectHandler(_ =>
            {
                disconnectHandled.Set();
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
            Assert.IsTrue(disconnectHandled.Wait(TimeSpan.FromSeconds(5)), "Post-handshake disconnects must reach the disconnect handler");
        }
    }
}
