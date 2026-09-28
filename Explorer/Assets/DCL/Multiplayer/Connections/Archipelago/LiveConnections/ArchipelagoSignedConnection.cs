using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using Decentraland.Kernel.Comms.V3;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    /// <summary>
    ///     Connection consists of signed handshake, and establishing web-socket connection <br />
    ///     Supports auto reconnection that will try to recover connection to the transport infinitely until it's cancelled
    /// </summary>
    public class ArchipelagoSignedConnection : IArchipelagoLiveConnection
    {
        private static readonly TimeSpan DEFAULT_RECOVERY_DELAY = TimeSpan.FromSeconds(5);

        private readonly TimeSpan recoveryDelay;

        private readonly IArchipelagoLiveConnection origin;

        private readonly DCLSemaphoreSlim semaphore = new ();

        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly SessionControl session;
        private int transportGeneration;
        private string? cachedAdapterUrl;

        private DateTime lastRecoveryAttempt = DateTime.MinValue;

        public bool IsConnected => origin.IsConnected;

        public ArchipelagoSignedConnection(IArchipelagoLiveConnection origin, TimeSpan recoveryDelay, IMultiPool multiPool, IMemoryPool memoryPool, IWeb3IdentityCache web3IdentityCache)
        {
            this.origin = origin;
            this.recoveryDelay = recoveryDelay;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
            this.web3IdentityCache = web3IdentityCache;
            session = SessionControl.For(web3IdentityCache);
        }

        public ArchipelagoSignedConnection(IArchipelagoLiveConnection origin, IMultiPool multiPool, IMemoryPool memoryPool, IWeb3IdentityCache web3IdentityCache) : this(origin, DEFAULT_RECOVERY_DELAY, multiPool, memoryPool, web3IdentityCache) { }

        public UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            cachedAdapterUrl = adapterUrl;
            return EnsureConnectionAsync(token);
        }

        public UniTask<Result> DisconnectAsync(CancellationToken token)
        {
            DCLInterlocked.Increment(ref transportGeneration);
            cachedAdapterUrl = null;
            return origin.DisconnectAsync(token);
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            int generation = session.Generation;
            while (true)
            {
                if (!session.CanRecover(generation)) return EnumResult<IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "Session suppressed");
                EnumResult<IArchipelagoLiveConnection.ResponseError> result = await origin.SendAsync(data, token);

                if (result.Error?.State is not IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
                    return result;

                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "Connection error on sending, ensure to reconnect...\n" + result.Error.Value.Message);
                Result connectionResult = await EnsureConnectionAsync(token);

                if (!connectionResult.Success)
                    return EnumResult<IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, connectionResult.ErrorMessage!);
            }
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            int logicalGeneration = session.Generation;
            while (true)
            {
                if (!session.CanListen(logicalGeneration)) return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "Session suppressed");
                int socketGeneration = DCLVolatile.Read(ref transportGeneration);
                EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await origin.ReceiveAsync(token);

                if (socketGeneration != DCLVolatile.Read(ref transportGeneration) || !session.CanListen(logicalGeneration))
                {
                    if (result.Success) result.Value.Dispose();
                    return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, "Stale socket callback");
                }

                if (result.Error?.State is not IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
                    return result;

                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "Connection error on receiving, ensure to reconnect...\n" + result.Error.Value.Message);

                Result connectionResult = await EnsureConnectionAsync(token);

                if (!connectionResult.Success)
                    return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, connectionResult.ErrorMessage!);
            }
        }

        private async UniTask<Result> EnsureConnectionAsync(CancellationToken token)
        {
            int generation = session.Generation;
            // Thus function must be entered only once, other calls should be waiting
            // Otherwise there is a race condition
            Result result = (await semaphore.WaitAsync(token).SuppressToResultAsync()).AsResult();

            if (!result.Success)
                return result;

            try
            {
                var attemptNumber = 1;

                if (!session.CanListen(generation)) return Result.ErrorResult("Session suppressed");

                if (origin.IsConnected) return Result.SuccessResult();

                result = Result.ErrorResult("Not Started");

                while (!origin.IsConnected)
                {
                    if (!session.CanListen(generation)) return Result.ErrorResult("Session suppressed");
                    if (!session.CanRecoverTransport(generation))
                    {
                        await UniTask.Delay(250, cancellationToken: token);
                        continue;
                    }
                    if (token.IsCancellationRequested)
                        return Result.CancelledResult();

                    if (cachedAdapterUrl == null)
                    {
                        // Wait for the adapter URL to be set
                        await UniTask.Yield();
                        continue;
                    }

                    await DelayRecoveryAsync(token);

                    if (!session.CanRecoverTransport(generation)) continue;

                    string adapter = cachedAdapterUrl!;
                    result = await WelcomePeerIdAsync(adapter, token);

                    if (!session.CanListen(generation))
                    {
                        await origin.DisconnectAsync(token);
                        return Result.ErrorResult("Session changed during authentication");
                    }

                    if (!result.Success)
                        ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, $"Cannot ensure connection to {adapter} after {attemptNumber} attempts: {result.ErrorMessage}");

                    attemptNumber++;
                    lastRecoveryAttempt = DateTime.Now;
                }

                return result;
            }
            finally { semaphore.Release(); }

            UniTask DelayRecoveryAsync(CancellationToken ct)
            {
                TimeSpan delay = recoveryDelay - (DateTime.Now - lastRecoveryAttempt);
                return delay.TotalMilliseconds > 0 ? UniTask.Delay(delay, cancellationToken: ct) : UniTask.CompletedTask;
            }
        }

        private async UniTask<Result<string>> WelcomePeerIdAsync(string adapterUrl, CancellationToken token)
        {
            int generation = session.Generation;
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();
            if (!session.CanListen(generation)) return Result<string>.ErrorResult("Session suppressed");
            IWeb3Identity identity = web3IdentityCache.EnsuredIdentity();

            Result result = await ReconnectAsync(adapterUrl, token);

            if (!result.Success || !session.CanListen(generation))
                return Result<string>.ErrorResult($"Cannot reconnect to {adapterUrl}: {result.ErrorMessage}");

            string ethereumAddress = identity.Address;
            Result<string> messageForSignResult = await MessageForSignAsync(ethereumAddress, token);

            if (!session.CanListen(generation) || messageForSignResult.Success == false ||
                !HandshakePayloadIsValid(messageForSignResult.Value))
                return Result<string>.ErrorResult("Cannot obtain a message to sign a welcome peer");

            string signedMessage;

            try { signedMessage = identity.Sign(messageForSignResult.Value).ToJson(); }
            catch (Exception e) { return Result<string>.ErrorResult($"Cannot sign message for welcome peer id: {e}"); }

            ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Signed message: {signedMessage}");
            return await ExecuteHandshakeAsync(signedMessage, generation, token);
        }

        private async UniTask<Result<string>> MessageForSignAsync(string ethereumAddress, CancellationToken token)
        {
            using SmartWrap<ChallengeRequestMessage> challenge = multiPool.TempResource<ChallengeRequestMessage>();
            challenge.value.Address = ethereumAddress;
            using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
            clientPacket.value.ClearMessage();
            clientPacket.value.ChallengeRequest = challenge.value;
            EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await origin.SendAndReceiveAsync(clientPacket.value, memoryPool, token);

            if (result.Success == false)
                return Result<string>.ErrorResult($"Cannot message for sign for address {ethereumAddress}: {result.Error?.Message}");

            using MemoryWrap response = result.Value;
            using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
            using var challengeResponse = new SmartWrap<ChallengeResponseMessage>(serverPacket.value.ChallengeResponse!, multiPool);
            return Result<string>.SuccessResult(challengeResponse.value.ChallengeToSign!);
        }

        private async UniTask<Result> ReconnectAsync(string adapterUrl, CancellationToken token)
        {
            DCLInterlocked.Increment(ref transportGeneration);
            Result result;

            if (origin.IsConnected)
            {
                result = await origin.DisconnectAsync(token);

                if (!result.Success)
                    return result;
            }

            result = await origin.ConnectAsync(adapterUrl, token);
            return result;
        }

        private async UniTask<Result<string>> ExecuteHandshakeAsync(string signedMessageAuthChainJson, int generation, CancellationToken token)
        {
            int socketGeneration = DCLVolatile.Read(ref transportGeneration);
            try
            {
                using SmartWrap<SignedChallengeMessage> signedMessage = multiPool.TempResource<SignedChallengeMessage>();
                signedMessage.value.AuthChainJson = signedMessageAuthChainJson;

                using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
                clientPacket.value.ClearMessage();
                clientPacket.value.SignedChallenge = signedMessage.value;

                // Consume the receive result even if socket state changes before its queued frame completes.
                EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result =
                    await origin.SendAndReceiveAsync(clientPacket.value, memoryPool, token);

                if (!result.Success)
                    return Result<string>.ErrorResult($"{nameof(ExecuteHandshakeAsync)}: {result.Error?.Message}");

                using MemoryWrap response = result.Value;
                if (!session.CanListen(generation) || socketGeneration != DCLVolatile.Read(ref transportGeneration))
                    return Result<string>.ErrorResult("Stale handshake response");

                using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
                switch (serverPacket.value.MessageCase)
                {
                    case ServerPacket.MessageOneofCase.Welcome:
                        using (var welcomeMessage = new SmartWrap<WelcomeMessage>(serverPacket.value.Welcome, multiPool))
                            return Result<string>.SuccessResult(welcomeMessage.value.PeerId);
                    case ServerPacket.MessageOneofCase.Kicked:
                        SessionControl.Status status = serverPacket.value.Kicked.Reason switch
                        {
                            KickedReason.KrBanned => SessionControl.Status.Banned,
                            KickedReason.KrNewSession => SessionControl.Status.Superseded,
                            _ => SessionControl.Status.Unknown,
                        };
                        session.Stop(generation, status);
                        return Result<string>.ErrorResult($"Handshake rejected: {status}");
                    default:
                        session.Stop(generation, SessionControl.Status.Unknown);
                        return Result<string>.ErrorResult($"Unexpected handshake response: {serverPacket.value.MessageCase}");
                }
            }
            catch (Exception e) { return Result<string>.ErrorResult($"Cannot complete signed handshake: {e}"); }
        }

        private bool HandshakePayloadIsValid(string payload)
        {
            if (!payload.StartsWith("dcl-"))
                return false;

            ReadOnlySpan<char> span = payload.AsSpan(4);
            return span.IndexOf(':') == -1;
        }
    }
}
