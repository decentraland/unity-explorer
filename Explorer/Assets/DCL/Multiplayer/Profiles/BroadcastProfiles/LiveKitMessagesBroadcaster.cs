using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Web3;
using Google.Protobuf;
using LiveKit.Rooms;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    /// <summary>
    ///     Provides a list of recipients and rooms for multiplayer-related messages
    /// </summary>
    public class LiveKitMessagesBroadcaster
    {
        /// <summary>
        ///     Hardcoded identity for the authoritative server in the LiveKit network.
        /// </summary>
        public const string AUTH_SERVER_IDENTITY = "authoritative-server";

        private readonly IGateKeeperSceneRoom sceneRoom;
        private readonly IMessagePipesHub messagePipesHub;

        /// <summary>
        ///     While Pulse is active, messages are sent only to the peers that announced their profiles over
        ///     LiveKit, excluding any peer that currently has a live Pulse session (it already receives the
        ///     message over Pulse). <see cref="SendProfileAnnouncement{TInput,TMessage}" /> periodically
        ///     forces a full untargeted broadcast instead, so peers that were never explicitly announced to
        ///     this client still get materialized. When Pulse is absent — disabled or fallen back — messages
        ///     are always broadcast to every peer in the rooms.
        /// </summary>
        private readonly PulseActivation pulseActivation;

        private readonly PeerIdCache peerIdCache;

        private readonly Dictionary<string, (RoomSource rooms, Web3Address wallet)> announcedWallets = new ();

        private readonly TimeSpan untargetedAnnounceInterval;
        private DateTime previousUntargetedAnnounce;

        public LiveKitMessagesBroadcaster(IGateKeeperSceneRoom sceneRoom, IMessagePipesHub messagePipesHub, PulseActivation pulseActivation, PeerIdCache peerIdCache)
            : this(sceneRoom, messagePipesHub, pulseActivation, peerIdCache, TimeSpan.FromSeconds(10)) { }

        public LiveKitMessagesBroadcaster(IGateKeeperSceneRoom sceneRoom, IMessagePipesHub messagePipesHub, PulseActivation pulseActivation, PeerIdCache peerIdCache, TimeSpan untargetedAnnounceInterval)
        {
            this.sceneRoom = sceneRoom;
            this.messagePipesHub = messagePipesHub;
            this.pulseActivation = pulseActivation;
            this.peerIdCache = peerIdCache;
            this.untargetedAnnounceInterval = untargetedAnnounceInterval;
        }

        public void SendProfileAnnouncement<TInput, TMessage>(Action<TInput, TMessage> buildMessage, TInput args,
            LKDataPacketKind packetKind, CancellationToken ct) where TMessage: class, IMessage, new()
        {
            if (pulseActivation.IsActive && DateTime.UtcNow - previousUntargetedAnnounce < untargetedAnnounceInterval)
            {
                Send(buildMessage, args, packetKind, ct);
                return;
            }

            previousUntargetedAnnounce = DateTime.UtcNow;
            SendUntargeted(buildMessage, args, packetKind, ct);
        }

        public void Send<TInput, TMessage>(Action<TInput, TMessage> buildMessage, TInput args,
            LKDataPacketKind packetKind, CancellationToken ct) where TMessage: class, IMessage, new()
        {
            if (pulseActivation.IsActive)
            {
                // Build up recipients lists for every room

                using PooledObject<List<string>> _ = ListPool<string>.Get(out List<string>? islandList);
                using PooledObject<List<string>> __ = ListPool<string>.Get(out List<string>? sceneList);

                foreach ((string walletId, (RoomSource rooms, Web3Address wallet)) in announcedWallets)
                {
                    if (peerIdCache.TryGetPeerId(wallet, out uint _))
                        continue;

                    if (EnumUtils.HasFlag(rooms, RoomSource.Island))
                        islandList.Add(walletId);

                    if (EnumUtils.HasFlag(rooms, RoomSource.Gatekeeper))
                        sceneList.Add(walletId);
                }

                if (sceneRoom.Room().Participants.RemoteParticipant(AUTH_SERVER_IDENTITY) != null)
                    sceneList.Add(AUTH_SERVER_IDENTITY);

                if (islandList.Count > 0)
                    BuildMessageAndSend(messagePipesHub.IslandPipe(), islandList, buildMessage, args, packetKind, ct);

                if (sceneList.Count > 0)
                    BuildMessageAndSend(messagePipesHub.ScenePipe(), sceneList, buildMessage, args, packetKind, ct);
            }
            else
            {
                // Broadcast as before
                SendUntargeted(buildMessage, args, packetKind, ct);
            }
        }

        private void SendUntargeted<TInput, TMessage>(Action<TInput, TMessage> buildMessage, TInput args,
            LKDataPacketKind packetKind, CancellationToken ct) where TMessage: class, IMessage, new()
        {
            BuildMessageAndSend(messagePipesHub.IslandPipe(), null, buildMessage, args, packetKind, ct);
            BuildMessageAndSend(messagePipesHub.ScenePipe(), null, buildMessage, args, packetKind, ct);
        }

        private void BuildMessageAndSend<TInput, TMessage>(IMessagePipe messagePipe, IReadOnlyList<string>? recipients,
            Action<TInput, TMessage> buildMessage, TInput args, LKDataPacketKind packetKind, CancellationToken ct) where TMessage: class, IMessage, new()
        {
            MessageWrap<TMessage> message = messagePipe.NewMessage<TMessage>();
            buildMessage(args, message.Payload);

            if (recipients != null)
                foreach (string recipient in recipients)
                    message.AddSpecialRecipient(recipient);

            message.SendAndDisposeAsync(ct, packetKind).Forget();
        }

        public void Add(string walletId, RoomSource from)
        {
            if (announcedWallets.TryGetValue(walletId, out (RoomSource rooms, Web3Address wallet) entry))
                announcedWallets[walletId] = (entry.rooms | from, entry.wallet);
            else
                announcedWallets[walletId] = (from, new Web3Address(walletId));
        }

        public void Remove(string walletId, RoomSource roomSource)
        {
            if (announcedWallets.TryGetValue(walletId, out (RoomSource rooms, Web3Address wallet) entry))
            {
                RoomSource currentSource = entry.rooms;
                currentSource.RemoveFlag(roomSource);

                if (currentSource == RoomSource.None)
                    announcedWallets.Remove(walletId);
                else
                    announcedWallets[walletId] = (currentSource, entry.wallet);
            }
        }
    }
}
