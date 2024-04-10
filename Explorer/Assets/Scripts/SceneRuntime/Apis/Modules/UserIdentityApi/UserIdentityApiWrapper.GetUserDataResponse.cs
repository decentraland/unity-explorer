using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.UserIdentityApi
{
    public partial class UserIdentityApiWrapper
    {
        [Serializable]
        public struct GetUserPublicKeyResponse
        {
            public string? address;

            public GetUserPublicKeyResponse(IWeb3IdentityCache identityCache)
            {
                address = identityCache.Identity?.Address;
            }
        }

        [Serializable]
        public class GetUserDataResponse
        {
            public Data? data;

            public GetUserDataResponse(Profile profile, IWeb3Identity identity, List<string> wearablesCache) : this(
                new Data(profile, identity, wearablesCache)
            ) { }

            public GetUserDataResponse(Data? data)
            {
                this.data = data;
            }

            [Serializable]
            public class Data
            {
                public string displayName;
                public string? publicKey;
                public bool hasConnectedWeb3;
                public string userId;
                public int version;
                public Avatar? avatar;

                public Data(Profile profile, IWeb3Identity identity, List<string> wearablesCache) : this(
                    profile.DisplayName,
                    identity.Address,
                    profile.HasConnectedWeb3,
                    profile.UserId,
                    profile.Version,
                    new Avatar(profile.Avatar, wearablesCache)
                ) { }

                public Data(string displayName, string? publicKey, bool hasConnectedWeb3, string userId, int version,
                    Avatar? avatar)
                {
                    this.displayName = displayName;
                    this.publicKey = publicKey;
                    this.hasConnectedWeb3 = hasConnectedWeb3;
                    this.userId = userId;
                    this.version = version;
                    this.avatar = avatar;
                }

                [Serializable]
                public class Avatar
                {
                    public string bodyShape;
                    public string eyeColor;
                    public string hairColor;
                    public string skinColor;
                    public List<string> wearables;
                    public Snapshot? snapshots;

                    public Avatar(DCL.Profiles.Avatar avatar, List<string> wearablesCache) : this(
                        avatar.BodyShape,
                        eyeColor: $"#{ColorUtility.ToHtmlStringRGB(avatar.EyesColor)}",
                        hairColor: $"#{ColorUtility.ToHtmlStringRGB(avatar.HairColor)}",
                        skinColor: $"#{ColorUtility.ToHtmlStringRGB(avatar.SkinColor)}",
                        wearablesCache,
                        new Snapshot(avatar.BodySnapshotUrl, avatar.FaceSnapshotUrl)
                    ) { }

                    public Avatar(string bodyShape, string eyeColor, string hairColor, string skinColor, List<string> wearables,
                        Snapshot snapshots)
                    {
                        this.bodyShape = bodyShape;
                        this.eyeColor = eyeColor;
                        this.hairColor = hairColor;
                        this.skinColor = skinColor;
                        this.wearables = wearables;
                        this.snapshots = snapshots;
                    }

                    [Serializable]
                    public class Snapshot
                    {
                        public string body;
                        public string face256;

                        public Snapshot(string body, string face256)
                        {
                            this.body = body;
                            this.face256 = face256;
                        }
                    }
                }
            }
        }
    }
}
