using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Rooms;
using Google.Protobuf;
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
        private readonly IMessagePipesHub messagePipesHub;

        /// <summary>
        ///     In the backward compatibility mode Profiles are only broadcasted to the peers that announced their profiles
        /// </summary>
        private readonly bool backwardCompatibilityMode;

        private readonly Dictionary<string, RoomSource> announcedWallets = new ();

        public LiveKitMessagesBroadcaster(IMessagePipesHub messagePipesHub, bool backwardCompatibilityMode)
        {
            this.messagePipesHub = messagePipesHub;
            this.backwardCompatibilityMode = backwardCompatibilityMode;
        }

        public void Send<TInput, TMessage>(Action<TInput, TMessage> buildMessage, TInput args,
            LKDataPacketKind packetKind, CancellationToken ct) where TMessage: class, IMessage, new()
        {
            if (backwardCompatibilityMode)
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

                if (islandList.Count > 0)
                    BuildMessageAndSend(messagePipesHub.IslandPipe(), islandList);

                if (sceneList.Count > 0)
                    BuildMessageAndSend(messagePipesHub.ScenePipe(), islandList);
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
