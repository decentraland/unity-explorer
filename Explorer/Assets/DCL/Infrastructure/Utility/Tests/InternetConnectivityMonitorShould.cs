using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Utility.Networking;

namespace Utility.Tests
{
    [TestFixture]
    public class InternetConnectivityMonitorShould
    {
        private const string LOOPBACK = "127.0.0.1";

        // RFC 2606 reserves .invalid so it never resolves; RFC 5737 reserves 192.0.2.0/24 so it is never routed
        private const string UNRESOLVABLE_HOST = "probe.invalid";
        private const string UNROUTABLE_HOST = "192.0.2.1";

        private static readonly TimeSpan SHORT_TIMEOUT = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan LONG_TTL = TimeSpan.FromMinutes(1);

        private TcpListener? listener;

        [TearDown]
        public void TearDown()
        {
            listener?.Stop();
            listener = null;
        }

        [Test]
        public async Task ReportOnlineWhenProbeHostAccepts()
        {
            // Arrange
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, StartListener(), SHORT_TIMEOUT);

            // Act
            bool online = await monitor.RefreshIfStaleAsync();

            // Assert
            Assert.IsTrue(online);
            Assert.IsTrue(monitor.IsOnline);
        }

        [Test]
        public async Task ReportOfflineWhenConnectionIsRefused()
        {
            // Arrange
            int closedPort = StartListener();
            listener!.Stop();
            listener = null;
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, closedPort, SHORT_TIMEOUT);

            // Act
            bool online = await monitor.RefreshIfStaleAsync();

            // Assert
            Assert.IsFalse(online);
            Assert.IsFalse(monitor.IsOnline);
        }

        [Test]
        public async Task ReportOfflineWhenHostDoesNotResolve()
        {
            using var monitor = new InternetConnectivityMonitor(UNRESOLVABLE_HOST, InternetConnectivityMonitor.HTTPS_PORT, SHORT_TIMEOUT);

            Assert.IsFalse(await monitor.RefreshIfStaleAsync());
        }

        [Test]
        public async Task ReportOfflineOnTimeout()
        {
            using var monitor = new InternetConnectivityMonitor(UNROUTABLE_HOST, InternetConnectivityMonitor.HTTPS_PORT, TimeSpan.FromMilliseconds(200));

            Assert.IsFalse(await monitor.RefreshIfStaleAsync());
        }

        [Test]
        public async Task CoalesceConcurrentProbesIntoOneConnection()
        {
            // Arrange
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, StartListener(), SHORT_TIMEOUT, LONG_TTL);

            // Act
            Task<bool> first = monitor.RefreshIfStaleAsync();
            Task<bool> second = monitor.RefreshIfStaleAsync();
            await first;

            // Assert
            Assert.AreSame(first, second);
            Assert.AreEqual(1, AcceptedConnections());
        }

        [Test]
        public async Task ReuseResultWithinTtl()
        {
            // Arrange
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, StartListener(), SHORT_TIMEOUT, LONG_TTL);
            await monitor.RefreshIfStaleAsync();
            AcceptedConnections();

            // Act
            Task<bool> cached = monitor.RefreshIfStaleAsync();

            // Assert
            Assert.IsTrue(cached.IsCompleted);
            Assert.IsTrue(await cached);
            Assert.AreEqual(0, AcceptedConnections());
        }

        [Test]
        public async Task ProbeAgainOnceTtlExpired()
        {
            // Arrange
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, StartListener(), SHORT_TIMEOUT, TimeSpan.Zero);

            // Act
            await monitor.RefreshIfStaleAsync();
            await monitor.RefreshIfStaleAsync();

            // Assert
            Assert.AreEqual(2, AcceptedConnections());
        }

        [Test]
        public async Task RecoverOnceProbeHostAcceptsAgain()
        {
            // Arrange
            int port = StartListener();
            listener!.Stop();
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, port, SHORT_TIMEOUT, TimeSpan.Zero);
            Assume.That(await monitor.RefreshIfStaleAsync(), Is.False);

            // Act
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            bool online = await monitor.RefreshIfStaleAsync();

            // Assert
            Assert.IsTrue(online);
        }

        [Test]
        public async Task NotProbeWhenStateIsRead()
        {
            // Arrange
            using var monitor = new InternetConnectivityMonitor(LOOPBACK, StartListener(), SHORT_TIMEOUT, TimeSpan.Zero);
            await monitor.RefreshIfStaleAsync();
            AcceptedConnections();

            // Act
            bool online = monitor.IsOnline;
            await Task.Delay(100);

            // Assert
            Assert.IsTrue(online);
            Assert.AreEqual(0, AcceptedConnections());
        }

        private int StartListener()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        /// <summary>Drains the listener's backlog; a connect that completed left exactly one entry in it.</summary>
        private int AcceptedConnections()
        {
            var accepted = 0;

            while (listener!.Pending())
            {
                listener.AcceptTcpClient().Dispose();
                accepted++;
            }

            return accepted;
        }
    }
}
