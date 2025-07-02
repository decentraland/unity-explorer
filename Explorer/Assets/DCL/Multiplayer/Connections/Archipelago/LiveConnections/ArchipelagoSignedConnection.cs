using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Decentraland.Kernel.Comms.V3;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using Utility.Multithreading;
using Utility.Types;

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

        private readonly SemaphoreSlim semaphore = new (1, 1);

        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly IWeb3IdentityCache web3IdentityCache;
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
        }

        public ArchipelagoSignedConnection(IArchipelagoLiveConnection origin, IMultiPool multiPool, IMemoryPool memoryPool, IWeb3IdentityCache web3IdentityCache) : this(origin, DEFAULT_RECOVERY_DELAY, multiPool, memoryPool, web3IdentityCache) { }

        public UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            cachedAdapterUrl = adapterUrl;
            return EnsureConnectionAsync(token);
        }

        public UniTask<Result> DisconnectAsync(CancellationToken token)
        {
            cachedAdapterUrl = null;
            return origin.DisconnectAsync(token);
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            while (true)
            {
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
            while (true)
            {
                EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await origin.ReceiveAsync(token);

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
            // Thus function must be entered only once, other calls should be waiting
            // Otherwise there is a race condition
            Result result = (await semaphore.WaitAsync(token).SuppressToResultAsync()).AsResult();

            if (!result.Success)
                return result;

            try
            {
                var attemptNumber = 1;

                if (origin.IsConnected) return Result.SuccessResult();

                result = Result.ErrorResult("Not Started");

                while (!origin.IsConnected)
                {
                    if (token.IsCancellationRequested)
                        return Result.CancelledResult();

                    if (cachedAdapterUrl == null)
                    {
                        // Wait for the adapter URL to be set
                        await UniTask.Yield();
                        continue;
                    }

                    await DelayRecoveryAsync(token);

                    string adapter = cachedAdapterUrl!;
                    result = await WelcomePeerIdAsync(adapter, token);

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
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();
            IWeb3Identity identity = web3IdentityCache.EnsuredIdentity();

            Result result = await ReconnectAsync(adapterUrl, token);

            if (!result.Success)
                return Result<string>.ErrorResult($"Cannot reconnect to {adapterUrl}: {result.ErrorMessage}");

            string ethereumAddress = identity.Address;
            Result<string> messageForSignResult = await MessageForSignAsync(ethereumAddress, token);

            if (messageForSignResult.Success == false ||
                !HandshakePayloadIsValid(messageForSignResult.Value))
                return Result<string>.ErrorResult("Cannot obtain a message to sign a welcome peer");

            string signedMessage;

            try { signedMessage = identity.Sign(messageForSignResult.Value).ToJson(); }
            catch (Exception e) { return Result<string>.ErrorResult($"Cannot sign message for welcome peer id: {e}"); }

            ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Signed message: {signedMessage}");
            return await ExecuteHandshakeAsync(signedMessage, token);
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

        private async UniTask<Result<string>> ExecuteHandshakeAsync(string signedMessageAuthChainJson, CancellationToken token)
        {
            try
            {
                using SmartWrap<SignedChallengeMessage> signedMessage = multiPool.TempResource<SignedChallengeMessage>();
                signedMessage.value.AuthChainJson = signedMessageAuthChainJson;

                using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
                clientPacket.value.ClearMessage();
                clientPacket.value.SignedChallenge = signedMessage.value;

                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource().Token, token);

                (bool hasResultLeft, EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result) result = await UniTask.WhenAny(
                    origin.SendAndReceiveAsync(clientPacket.value, memoryPool, linkedToken.Token),
                    origin.WaitDisconnectAsync(linkedToken.Token)
                );

                linkedToken.Cancel();

                if (result.hasResultLeft)
                {
                    if (result.result.Success == false)
                        return Result<string>.ErrorResult($"{nameof(ExecuteHandshakeAsync)}: {result.result.Error?.Message}");

                    using MemoryWrap response = result.result.Value;
                    using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
                    using var welcomeMessage = new SmartWrap<WelcomeMessage>(serverPacket.value.Welcome!, multiPool);
                    return Result<string>.SuccessResult(welcomeMessage.value.PeerId);
                }

                return Result<string>.ErrorResult($"{nameof(ExecuteHandshakeAsync)}: Disconnected during handshake");
            }
            catch (Exception e) { return Result<string>.ErrorResult($"Cannot welcome peer id for signed message {signedMessageAuthChainJson}: {e}"); }
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
