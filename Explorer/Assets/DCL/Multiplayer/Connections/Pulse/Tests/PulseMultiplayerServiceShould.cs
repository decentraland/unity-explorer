using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse.Tests
{
    [TestFixture]
    public class PulseMultiplayerServiceShould
    {
        private ITransport transport;
        private IDecentralandUrlsSource urlsSource;
        private PulseMultiplayerService service;
        private CancellationTokenSource cts;

        [SetUp]
        public void SetUp()
        {
            transport = Substitute.For<ITransport>();
            transport.State.Returns(ITransport.TransportState.NONE);

            urlsSource = Substitute.For<IDecentralandUrlsSource>();

            service = new PulseMultiplayerService(transport, new MessagePipe(), urlsSource);
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
    }
}
