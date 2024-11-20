using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.UsersMarker
{
    public class RemotePlayersDTOs
    {
        [Serializable]
        public class RemotePlayerData
        {
            public Vector3 position;
            public string avatarId;
        }
    }
}
