using CrdtEcsBridge.Components;
using DCL.Optimization.ThreadSafePool;
using Decentraland.Pulse;
using Google.Protobuf;
using System;
using System.Collections.Generic;
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
        };

        public ClientMessage Get(ClientMessage.MessageOneofCase kind) =>
            POOLS[kind].Get();

        public void Release(ClientMessage message)
        {
            POOLS[message.MessageCase].Release(message);
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
        };

        public ServerMessage Get(ServerMessage.MessageOneofCase kind) =>
            POOLS[kind].Get();

        public void Release(ServerMessage message)
        {
            POOLS[message.MessageCase].Release(message);
        }
    }
}
