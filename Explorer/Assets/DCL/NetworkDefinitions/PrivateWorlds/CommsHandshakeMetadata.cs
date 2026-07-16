using System;
using UnityEngine;

namespace DCL.PrivateWorlds
{
    public static class CommsHandshakeMetadata
    {
        public const string INTENT = "dcl:explorer:comms-handshake";
        public const string SIGNER = "dcl:explorer";

        public static string BuildWorldJson(string secret)
        {
            var metadata = new MetadataWithSecret
            {
                intent = INTENT,
                signer = SIGNER,
                isGuest = false,
                secret = secret,
            };

            return JsonUtility.ToJson(metadata);
        }
        
        public static string BuildSceneJson(string realmName, string realmServerName, string? sceneId, string secret)
        {
            var metadata = new SceneMetadataWithSecret
            {
                realmName = realmName,
                realm = new SceneRealm { serverName = realmServerName },
                sceneId = sceneId,
                intent = INTENT,
                signer = SIGNER,
                isGuest = false,
                secret = secret,
            };

            return JsonUtility.ToJson(metadata);
        }

        [Serializable]
        private struct MetadataWithSecret
        {
            public string intent;
            public string signer;
            public bool isGuest;
            public string secret;
        }

        [Serializable]
        private struct SceneMetadataWithSecret
        {
            public string realmName;
            public SceneRealm realm;
            public string? sceneId;
            public string intent;
            public string signer;
            public bool isGuest;
            public string secret;
        }

        [Serializable]
        private struct SceneRealm
        {
            public string serverName;
        }
    }
}
