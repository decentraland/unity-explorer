using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.UserIdentityApi
{
    public partial class UserIdentityApiWrapper
    {
        [Serializable]
        public struct GetUserPublicKeyResponse
        {
            public string? address;
        }

        [Serializable]
        public struct GetUserDataResponse
        {
            public Data data;

            [Serializable]
            public struct Data
            {
                public string publicKey;
                public string displayName;
                public bool hasConnectedWeb3;
                public string userId;
                public int version;
                public Avatar avatar;

                [Serializable]
                public struct Avatar
                {
                    public string bodyShape;
                    public string eyeColor;
                    public string hairColor;
                    public string skinColor;
                    public List<string> wearables;
                    public Snapshot snapshots;

                    [Serializable]
                    public struct Snapshot
                    {
                        public string body;
                        public string face256;
                    }
                }
            }
        }
    }
}
