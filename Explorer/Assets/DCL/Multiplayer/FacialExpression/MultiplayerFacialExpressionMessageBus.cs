using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using Decentraland.Kernel.Comms.Rfc4;
using DCL.LiveKit.Public;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Multiplayer.FacialExpression
{
    /// <summary>
    ///     Carries per-player <see cref="FacialExpression"/> state across the rfc4 comms.
    ///     Edge-triggered: <see cref="Send"/> is only called when the local player's indices change
    ///     (see ADR-317). Last-received message per peer is the authoritative remote state.
    /// </summary>
    public class MultiplayerFacialExpressionMessageBus : IFacialExpressionMessageBus, IDisposable
    {
        private const byte MAX_INDEX = 15;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly CancellationTokenSource cts = new ();
        private readonly HashSet<RemoteFacialExpressionIntention> intentions = new ();

        public MultiplayerFacialExpressionMessageBus(IMessagePipesHub messagePipesHub)
        {
            this.messagePipesHub = messagePipesHub;

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.FacialExpression>(Packet.MessageOneofCase.FacialExpression, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.FacialExpression>(Packet.MessageOneofCase.FacialExpression, OnMessageReceived);
        }

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();
        }

        public void Send(byte eyebrowsIndex, byte eyesIndex, byte mouthIndex)
        {
            if (cts.IsCancellationRequested)
                return;

            SendTo(eyebrowsIndex, eyesIndex, mouthIndex, messagePipesHub.IslandPipe());
            SendTo(eyebrowsIndex, eyesIndex, mouthIndex, messagePipesHub.ScenePipe());
        }

        public void Drain(ICollection<RemoteFacialExpressionIntention> output)
        {
            foreach (RemoteFacialExpressionIntention intention in intentions)
                output.Add(intention);

            intentions.Clear();
        }

        public void SaveForRetry(RemoteFacialExpressionIntention intention) =>
            intentions.Add(intention);

        private void SendTo(byte eyebrows, byte eyes, byte mouth, IMessagePipe pipe)
        {
            MessageWrap<Decentraland.Kernel.Comms.Rfc4.FacialExpression> message = pipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.FacialExpression>();

            message.Payload.EyebrowsIndex = eyebrows;
            message.Payload.EyesIndex = eyes;
            message.Payload.MouthIndex = mouth;

            message.SendAndDisposeAsync(cts.Token, LKDataPacketKind.KindReliable).Forget();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.FacialExpression> receivedMessage)
        {
            using (receivedMessage)
            {
                if (cts.IsCancellationRequested)
                    return;

                var payload = receivedMessage.Payload;

                // Per ADR-317: drop messages with out-of-range indices.
                if (payload.EyebrowsIndex > MAX_INDEX || payload.EyesIndex > MAX_INDEX || payload.MouthIndex > MAX_INDEX)
                    return;

                intentions.Add(new RemoteFacialExpressionIntention(
                    receivedMessage.FromWalletId,
                    (byte)payload.EyebrowsIndex,
                    (byte)payload.EyesIndex,
                    (byte)payload.MouthIndex));
            }
        }
    }
}