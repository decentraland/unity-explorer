using CrdtEcsBridge.Components;
using DCL.Optimization.ThreadSafePool;
using Decentraland.Pulse;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class ClientMessagePool
    {
        private static IObjectPool<ClientMessage> CreatePool<T>(Action<ClientMessage, T> setData) where T: class, IMessage, new()
        {
            return new ThreadSafeObjectPool<ClientMessage>(() =>
            {
                var packet = new ClientMessage();

                // Preallocated once and merged with after
                setData.Invoke(packet, new T());
                return packet;
            }, actionOnRelease: packet => { packet.GetUnderlyingData()?.ClearProtobufComponent(); });
        }

        private static readonly IReadOnlyDictionary<ClientMessage.MessageOneofCase, IObjectPool<ClientMessage>> POOLS = new Dictionary<ClientMessage.MessageOneofCase, IObjectPool<ClientMessage>>
        {
            [ClientMessage.MessageOneofCase.Handshake] = CreatePool<HandshakeRequest>((packet, mes) => packet.Handshake = mes),
            [ClientMessage.MessageOneofCase.Input] = CreatePool<PlayerStateInput>((packet, mes) => packet.Input = mes),
            [ClientMessage.MessageOneofCase.Resync] = CreatePool<PlayerStateInput>((packet, mes) => packet.Input = mes),
            [ClientMessage.MessageOneofCase.ProfileAnnouncement] = CreatePool<PlayerStateInput>((packet, mes) => packet.Input = mes),
            [ClientMessage.MessageOneofCase.EmoteStart] = CreatePool<EmoteStart>((packet, mes) => packet.EmoteStart = mes),
            [ClientMessage.MessageOneofCase.EmoteStop] = CreatePool<EmoteStop>((packet, mes) => packet.EmoteStop = mes),
            [ClientMessage.MessageOneofCase.Teleport] = CreatePool<TeleportRequest>((packet, mes) => packet.Teleport = mes),
        };

        public ClientMessage Get(ClientMessage.MessageOneofCase kind)
        {
            if (POOLS.TryGetValue(kind, out IObjectPool<ClientMessage> pool))
                return pool.Get();

            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Pulse has no client message pool for this kind");
        }

        public void Release(ClientMessage message)
        {
            // A message of a kind without a pool was not taken from one, so it is left to the GC
            if (POOLS.TryGetValue(message.MessageCase, out IObjectPool<ClientMessage> pool))
                pool.Release(message);
        }
    }

    public class ServerMessagePool
    {
        private static IObjectPool<ServerMessage> CreatePool<T>(Action<ServerMessage, T> setData) where T: class, IMessage, new()
        {
            return new ThreadSafeObjectPool<ServerMessage>(() =>
            {
                var packet = new ServerMessage();

                // Preallocated once and merged with after
                setData.Invoke(packet, new T());
                return packet;
            }, actionOnRelease: packet => { packet.GetUnderlyingData()?.ClearProtobufComponent(); });
        }

        private static readonly IReadOnlyDictionary<ServerMessage.MessageOneofCase, IObjectPool<ServerMessage>> POOLS = new Dictionary<ServerMessage.MessageOneofCase, IObjectPool<ServerMessage>>
        {
            [ServerMessage.MessageOneofCase.Handshake] = CreatePool<HandshakeResponse>((packet, mes) => packet.Handshake = mes),
            [ServerMessage.MessageOneofCase.PlayerJoined] = CreatePool<PlayerJoined>((packet, mes) => packet.PlayerJoined = mes),
            [ServerMessage.MessageOneofCase.PlayerStateDelta] = CreatePool<PlayerStateDeltaTier0>((packet, mes) => packet.PlayerStateDelta = mes),
            [ServerMessage.MessageOneofCase.PlayerStateFull] = CreatePool<PlayerStateFull>((packet, mes) => packet.PlayerStateFull = mes),
            [ServerMessage.MessageOneofCase.PlayerLeft] = CreatePool<PlayerLeft>((packet, mes) => packet.PlayerLeft = mes),
            [ServerMessage.MessageOneofCase.PlayerProfileVersionAnnounced] = CreatePool<PlayerProfileVersionsAnnounced>((packet, mes) => packet.PlayerProfileVersionAnnounced = mes),
            [ServerMessage.MessageOneofCase.EmoteStarted] = CreatePool<EmoteStarted>((packet, mes) => packet.EmoteStarted = mes),
            [ServerMessage.MessageOneofCase.EmoteStopped] = CreatePool<EmoteStopped>((packet, mes) => packet.EmoteStopped = mes),
            [ServerMessage.MessageOneofCase.Teleported] = CreatePool<TeleportPerformed>((packet, mes) => packet.Teleported = mes),
        };

        public bool TryGet(ServerMessage.MessageOneofCase kind, [MaybeNullWhen(false)] out ServerMessage message)
        {
            if (POOLS.TryGetValue(kind, out IObjectPool<ServerMessage> pool))
            {
                message = pool.Get();
                return true;
            }

            message = null;
            return false;
        }

        public void Release(ServerMessage message)
        {
            // A message of a kind without a pool was not taken from one, so it is left to the GC
            if (POOLS.TryGetValue(message.MessageCase, out IObjectPool<ServerMessage> pool))
                pool.Release(message);
        }
    }
}
