using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using Decentraland.Pulse;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Keeps pools per type of the underlying message separated so
    ///     they never overlap and assign to the same field within the same pool
    /// </summary>
    public class PacketPool<TPacketType> where TPacketType: class, IMessage, new()
    {
        private readonly ConcurrentDictionary<Type, IObjectPool<TPacketType>> pools = new ();

        private readonly IReadOnlyDictionary<Type, Action<TPacketType, IMessage>> setMessageToPacket;

        public PacketPool(IReadOnlyDictionary<Type, Action<TPacketType, IMessage>> setMessageToPacket)
        {
            this.setMessageToPacket = setMessageToPacket;
        }

        public TPacketType Get<T>() where T: class, IMessage, new()
        {
            if (!pools.TryGetValue(typeof(T), out IObjectPool<TPacketType>? pool))
                pool = pools[typeof(T)] = new ThreadSafeObjectPool<TPacketType>(() =>
                {
                    var packet = new TPacketType();
                    setMessageToPacket[typeof(T)](packet, new T());
                    return packet;
                });

            return pool.Get();
        }

        public void Release(Type type, TPacketType message)
        {
            if (!pools.TryGetValue(type, out IObjectPool<TPacketType>? pool))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Pool for {type} was not created");
                return;
            }

            pool.Release(message);
        }
    }

    public class ClientMessagePool : PacketPool<ClientMessage>
    {
        public ClientMessagePool() : base(new Dictionary<Type, Action<ClientMessage, IMessage>>
        {
            [typeof(HandshakeRequest)] = (packet, message) => packet.Handshake = (HandshakeRequest)message,
            [typeof(PlayerStateInput)] = (packet, message) => packet.Input = (PlayerStateInput)message,
        }) { }
    }
}
