using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Meta
{
    [Serializable]
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public struct MetaData
    {
        public string realmName;
        public string sceneId;

        public MetaData(string realmName, string sceneId)
        {
            this.realmName = realmName;
            this.sceneId = sceneId;
        }

        public string ToJson() =>
            JsonUtility.ToJson(this)!;
    }
}
