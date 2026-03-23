using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Optimization.Multithreading;
using DCL.Optimization.Pools;
using Decentraland.Pulse;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Systems
{
    public class PulseEmotesMessageBus : IEmotesMessageBus, IDisposable
    {
        private readonly PulseMultiplayerService pulseService;

        private readonly HashSet<RemoteEmoteIntention> emoteIntentions = new (PoolConstants.AVATARS_COUNT);
        private readonly MutexSync sync = new ();

        public PulseEmotesMessageBus(PulseMultiplayerService pulseService)
        {
            this.pulseService = pulseService;
        }

        public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
            new (sync, emoteIntentions);

        public void Send(URN urn, bool loopCyclePassed, uint durationMs = 0)
        {
            if (loopCyclePassed)
                return;

            var outgoing = MessagePipe.OutgoingMessage.Create(
                ITransport.PacketMode.RELIABLE,
                ClientMessage.MessageOneofCase.EmoteStart);

            outgoing.Message.EmoteStart.EmoteId = urn;

            if (durationMs > 0)
                outgoing.Message.EmoteStart.DurationMs = durationMs;

            pulseService.Send(outgoing);
        }

        public void SendStop()
        {
            var outgoing = MessagePipe.OutgoingMessage.Create(
                ITransport.PacketMode.RELIABLE,
                ClientMessage.MessageOneofCase.EmoteStop);

            pulseService.Send(outgoing);
        }

        public void OnPlayerRemoved(string walletId) { }

        public void SaveForRetry(RemoteEmoteIntention intention)
        {
            using (sync.GetScope())
                emoteIntentions.Add(intention);
        }

        internal void Enqueue(RemoteEmoteIntention intention)
        {
            using (sync.GetScope())
                emoteIntentions.Add(intention);
        }

        public void Dispose() { }
    }
}