using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using Google.Protobuf;
using Newtonsoft.Json;
using Pulse.Transport;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        private readonly Dictionary<string, string> authChainBuffer = new ();

        private bool hasConnectedBefore;

        /// <summary>
        ///     Runs the post-transport-connect handshake exchange. Registers a one-shot response
        ///     handler, sends a <c>HandshakeRequest</c> (with auth chain + optional
        ///     <c>PlayerInitialState</c> on reconnect), and awaits the matching
        ///     <c>HandshakeResponse</c>. Throws <see cref="PulseException" /> on failure — the
        ///     service then propagates it out of <c>ConnectAsync</c>.
        /// </summary>
        private async UniTask HandshakeAsync(UniTaskCompletionSource<(bool success, string? error)> handshakeReceived, CancellationToken ct)
        {
            var handshakePacket = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Handshake);
            handshakePacket.Message.Handshake.AuthChain = ByteString.CopyFromUtf8(BuildAuthChain());

            handshakePacket.Message.Handshake.ProfileVersion =
                WriteInitialState(handshakePacket.Message.Handshake);

            pulseService.Send(handshakePacket);

            (bool success, string? error) = await handshakeReceived.Task;

            if (!success)
            {
                pulseService.Disconnect();
                throw new PulseException(error ?? "Handshake failed");
            }

            hasConnectedBefore = true;
        }

        private string BuildAuthChain()
        {
            authChainBuffer.Clear();

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using AuthChain authChain = identityCache.EnsuredIdentity().Sign($"connect:/:{timestamp}:{{}}");
            var authChainIndex = 0;

            foreach (AuthLink link in authChain)
            {
                authChainBuffer[$"x-identity-auth-chain-{authChainIndex}"] = link.ToJson();
                authChainIndex++;
            }

            authChainBuffer["x-identity-timestamp"] = timestamp.ToString();
            authChainBuffer["x-identity-metadata"] = "{}";

            return JsonConvert.SerializeObject(authChainBuffer);
        }
    }
}
