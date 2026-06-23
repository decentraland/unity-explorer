using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.Rooms;
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
        ///     LiveKit (the rest receive them over Pulse). When Pulse is absent — disabled or fallen back —
        ///     messages are broadcast to every peer in the rooms.
        /// </summary>
        private readonly PulseActivation pulseActivation;

        private readonly Dictionary<string, RoomSource> announcedWallets = new ();

        public LiveKitMessagesBroadcaster(IGateKeeperSceneRoom sceneRoom, IMessagePipesHub messagePipesHub, PulseActivation pulseActivation)
        {
            this.sceneRoom = sceneRoom;
            this.messagePipesHub = messagePipesHub;
            this.pulseActivation = pulseActivation;
        }

        public void Send<TInput, TMessage>(Action<TInput, TMessage> buildMessage, TInput args,
            LKDataPacketKind packetKind, CancellationToken ct) where TMessage: class, IMessage, new()
        {
            if (pulseActivation.IsActive)
            {
                // Build up recipients lists for every room

                using PooledObject<List<string>> _ = ListPool<string>.Get(out List<string>? islandList);
                using PooledObject<List<string>> __ = ListPool<string>.Get(out List<string>? sceneList);

                foreach ((string walletId, RoomSource rooms) in announcedWallets)
                {
                    if (EnumUtils.HasFlag(rooms, RoomSource.ISLAND))
                        islandList.Add(walletId);

                    if (EnumUtils.HasFlag(rooms, RoomSource.GATEKEEPER))
                        sceneList.Add(walletId);
                }

                if (sceneRoom.Room().Participants.RemoteParticipant(AUTH_SERVER_IDENTITY) != null)
                    sceneList.Add(AUTH_SERVER_IDENTITY);

                if (islandList.Count > 0)
                    BuildMessageAndSend(messagePipesHub.IslandPipe(), islandList);

                if (sceneList.Count > 0)
                    BuildMessageAndSend(messagePipesHub.ScenePipe(), sceneList);
            }
            else
            {
                // Broadcast as before
                BuildMessageAndSend(messagePipesHub.IslandPipe(), null);
                BuildMessageAndSend(messagePipesHub.ScenePipe(), null);
            }

            void BuildMessageAndSend(IMessagePipe messagePipe, IReadOnlyList<string>? recipients)
            {
                MessageWrap<TMessage> message = messagePipe.NewMessage<TMessage>();
                buildMessage(args, message.Payload);

                if (recipients != null)
                    foreach (string recipient in recipients)
                        message.AddSpecialRecipient(recipient);

                message.SendAndDisposeAsync(ct, packetKind).Forget();
            }
        }

        public void Add(string walletId, RoomSource from)
        {
            if (announcedWallets.TryGetValue(walletId, out RoomSource source))
                from |= source;

            announcedWallets[walletId] = from;
        }

        public void Remove(string walletId, RoomSource roomSource)
        {
            if (announcedWallets.TryGetValue(walletId, out RoomSource currentSource))
            {
                currentSource.RemoveFlag(roomSource);

                if (currentSource == RoomSource.NONE)
                    announcedWallets.Remove(walletId);
                else
                    announcedWallets[walletId] = currentSource;
            }
        }
    }
}
