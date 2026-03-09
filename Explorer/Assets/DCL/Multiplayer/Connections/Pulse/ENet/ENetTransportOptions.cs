using System;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    [Serializable]
    public sealed class ENetTransportOptions
    {
        [field: SerializeField]
        public int ServiceTimeoutMs { get; set; } = 1;

        [field: SerializeField]
        public int BufferSize { get; set; } = 4096;
    }
}
