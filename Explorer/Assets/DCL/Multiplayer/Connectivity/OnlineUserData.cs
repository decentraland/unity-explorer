using System;
using UnityEngine;

namespace DCL.Multiplayer.Connectivity
{
    [Serializable]
    public struct OnlineUserData
    {
        public Vector3 position;
        public string avatarId;

        public override int GetHashCode() =>
            avatarId.GetHashCode();

        public override string ToString() =>
            avatarId;
    }
}