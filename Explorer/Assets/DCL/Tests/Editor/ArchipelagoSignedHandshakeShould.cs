using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Pools;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using DCL.Web3.Accounts.Factory;
using DCL.Web3.Authenticators;
using DCL.Web3.Chains;
using Decentraland.Kernel.Comms.V3;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools.Memory;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Utility.Multithreading;

namespace DCL.Tests.Editor
{
    public class ArchipelagoSignedHandshakeShould
    {
        [TestCase(KickedReason.KrBanned, SessionControl.Status.Banned, false)]
        [TestCase(KickedReason.KrNewSession, SessionControl.Status.Superseded, false)]
        [TestCase((KickedReason)99, SessionControl.Status.Unknown, false)]
        [TestCase(KickedReason.KrBanned, SessionControl.Status.Banned, true)]
        public async Task StopOnSignedChallengeKickBeforeWelcome(KickedReason reason, SessionControl.Status expected, bool closeBeforeDelivery)
        {
            // Arrange
            using var identity = NewSigningIdentity();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = identity;
            var session = SessionControl.For(cache);
            var origin = new HandshakeTransport(new ServerPacket { Kicked = new KickedMessage { Reason = reason } }, closeBeforeDelivery);
            var connection = new ArchipelagoSignedConnection(origin, TimeSpan.Zero, new DCLMultiPool(), new ArrayMemoryPool(), cache);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            try { Assert.IsFalse((await connection.ConnectAsync("wss://handshake.test", cts.Token)).Success); }
            catch (OperationCanceledException) { }

            // Assert
            Assert.AreEqual(expected, session.Current);
            Assert.IsFalse(cts.IsCancellationRequested, "Terminal response must finish without waiting for cancellation");
            Assert.AreEqual(1, origin.ConnectCount);
            Assert.AreEqual(1, origin.SignedChallengeCount);
            Assert.IsFalse((await connection.ConnectAsync("wss://handshake.test", CancellationToken.None)).Success);
            Assert.AreEqual(1, origin.ConnectCount, "Terminal control must suppress subsequent connect calls too");
            Assert.IsFalse(session.AcceptAssignment(session.Generation));
            if (expected == SessionControl.Status.Banned) Assert.IsFalse(session.BeginReauthentication());
        }

        [Test]
        public async Task AcceptOrdinaryWelcome()
        {
            // Arrange
            using var identity = NewSigningIdentity();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = identity;
            var origin = new HandshakeTransport(new ServerPacket { Welcome = new WelcomeMessage { PeerId = "peer" } });
            var connection = new ArchipelagoSignedConnection(origin, TimeSpan.Zero, new DCLMultiPool(), new ArrayMemoryPool(), cache);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            Result result = await connection.ConnectAsync("wss://handshake.test", cts.Token);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(SessionControl.Status.Active, SessionControl.For(cache).Current);
            Assert.AreEqual(1, origin.SignedChallengeCount);
        }

        [Test]
        public async Task RejectUnexpectedHandshakePacket()
        {
            // Arrange
            using var identity = NewSigningIdentity();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = identity;
            var origin = new HandshakeTransport(new ServerPacket());
            var connection = new ArchipelagoSignedConnection(origin, TimeSpan.Zero, new DCLMultiPool(), new ArrayMemoryPool(), cache);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            Assert.IsFalse((await connection.ConnectAsync("wss://handshake.test", cts.Token)).Success);

            // Assert
            Assert.AreEqual(SessionControl.Status.Unknown, SessionControl.For(cache).Current);
            Assert.AreEqual(1, origin.ConnectCount);
        }

        [Test]
        public async Task RecoverOrdinaryDisconnectWithoutInventingTerminalControl()
        {
            // Arrange
            using var identity = NewSigningIdentity();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = identity;
            var origin = new HandshakeTransport(new ServerPacket { Welcome = new WelcomeMessage { PeerId = "peer" } }, disconnectFirstReply: true);
            var connection = new ArchipelagoSignedConnection(origin, TimeSpan.Zero, new DCLMultiPool(), new ArrayMemoryPool(), cache);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            Assert.IsTrue((await connection.ConnectAsync("wss://handshake.test", cts.Token)).Success);

            // Assert
            Assert.AreEqual(SessionControl.Status.Active, SessionControl.For(cache).Current);
            Assert.AreEqual(2, origin.ConnectCount);
            Assert.AreEqual(2, origin.SignedChallengeCount);
        }

        [Test]
        public async Task IgnoreOldHandshakeKickAfterIdentityChanges()
        {
            // Arrange
            using var oldIdentity = NewSigningIdentity();
            using var newIdentity = NewSigningIdentity();
            using var cache = new MemoryWeb3IdentityCache();
            cache.Identity = oldIdentity;
            var origin = new HandshakeTransport(new ServerPacket { Kicked = new KickedMessage { Reason = KickedReason.KrBanned } }, holdResponse: true);
            var connection = new ArchipelagoSignedConnection(origin, TimeSpan.Zero, new DCLMultiPool(), new ArrayMemoryPool(), cache);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            // Act
            UniTask<Result> connecting = connection.ConnectAsync("wss://handshake.test", cts.Token);
            await origin.Signed.Task.AttachExternalCancellation(cts.Token);
            cache.Identity = newIdentity;
            origin.Release.TrySetResult();
            Assert.IsFalse((await connecting).Success);

            // Assert
            Assert.AreEqual(SessionControl.Status.Active, SessionControl.For(cache).Current);
        }

        private static DecentralandIdentity NewSigningIdentity()
        {
            var account = new Web3AccountFactory().CreateRandomAccount();
            var chain = AuthChain.Create();
            chain.Set(new AuthLink { type = AuthLinkType.SIGNER, payload = account.Address.ToString(), signature = string.Empty });
            chain.Set(new AuthLink { type = AuthLinkType.ECDSA_EPHEMERAL, payload = "handshake-test", signature = account.Sign("handshake-test") });
            return new DecentralandIdentity(account.Address, account, DateTime.UtcNow.AddHours(1), chain, LoginMethod.ANY);
        }

        private sealed class HandshakeTransport : IArchipelagoLiveConnection
        {
            public readonly UniTaskCompletionSource Signed = new ();
            public readonly UniTaskCompletionSource Release = new ();

            private readonly ServerPacket response;
            private readonly bool closeBeforeDelivery;
            private readonly bool holdResponse;
            private readonly bool disconnectFirstReply;
            private readonly Atomic<bool> connected = new ();
            private readonly ArrayMemoryPool pool = new ();
            private ClientPacket.MessageOneofCase sent;

            public int ConnectCount { get; private set; }
            public int SignedChallengeCount { get; private set; }
            public bool IsConnected => connected.Value();

            public HandshakeTransport(ServerPacket response, bool closeBeforeDelivery = false, bool holdResponse = false, bool disconnectFirstReply = false)
            {
                this.response = response;
                this.closeBeforeDelivery = closeBeforeDelivery;
                this.holdResponse = holdResponse;
                this.disconnectFirstReply = disconnectFirstReply;
            }

            public UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
            {
                ConnectCount++;
                connected.Set(true);
                return UniTask.FromResult(Result.SuccessResult());
            }

            public UniTask<Result> DisconnectAsync(CancellationToken token)
            {
                connected.Set(false);
                return UniTask.FromResult(Result.SuccessResult());
            }

            public UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
            {
                ClientPacket packet = ClientPacket.Parser.ParseFrom(data.Span());
                sent = packet.MessageCase;
                if (sent == ClientPacket.MessageOneofCase.SignedChallenge)
                {
                    Assert.IsNotEmpty(packet.SignedChallenge.AuthChainJson);
                    SignedChallengeCount++;
                    Signed.TrySetResult();
                }
                return UniTask.FromResult(EnumResult<IArchipelagoLiveConnection.ResponseError>.SuccessResult());
            }

            public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
            {
                ServerPacket packet;
                if (sent == ClientPacket.MessageOneofCase.ChallengeRequest)
                    packet = new ServerPacket { ChallengeResponse = new ChallengeResponseMessage { ChallengeToSign = "dcl-handshake-test" } };
                else
                {
                    if (disconnectFirstReply && SignedChallengeCount == 1)
                    {
                        connected.Set(false);
                        return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                            IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "Ordinary transport close");
                    }
                    if (closeBeforeDelivery)
                    {
                        connected.Set(false);
                        await UniTask.Delay(30, cancellationToken: token);
                    }
                    // The stale-session case releases this await after replacing the identity.
                    if (holdResponse) await Release.Task.AttachExternalCancellation(token);
                    packet = response;
                }
                MemoryWrap memory = pool.Memory(packet.CalculateSize());
                packet.WriteTo(memory.Span());
                return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.SuccessResult(memory);
            }
        }
    }
}
