using System;
using Newtonsoft.Json;
using UnityEngine;

namespace DCL.Multiplayer.Connectivity
{
    [Serializable]
    public struct OnlineUserData
    {
        public bool IsInWorld => !string.IsNullOrEmpty(worldName);
        public Vector3 position;
        [JsonProperty("world")]
        public string? worldName;
        [JsonProperty("wallet")]
        public string avatarId;

        public override int GetHashCode() =>
            avatarId.GetHashCode();

        public override string ToString() =>
            avatarId;
    }
}
