using System;
using UnityEngine;

namespace DCL.Multiplayer.Connectivity
{
    [Serializable]
    public struct OnlineUserData
    {
        public bool IsInWorld => !string.IsNullOrEmpty(worldName);
        public Vector3 position;
        public string? worldName;
        public string avatarId;

        public override int GetHashCode() =>
            avatarId.GetHashCode();

        public override string ToString() =>
            avatarId;
    }
}
