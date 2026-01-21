using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using JetBrains.Annotations;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.Apis.Modules.UserIdentityApi
{
    public partial class UserIdentityApiWrapper : JsApiWrapper
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly List<string> wearablesCache = new ();

        public UserIdentityApiWrapper(IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache,
            ISceneExceptionsHandler sceneExceptionsHandler,
            CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/UserIdentity.js ")]
        public object UserPublicKey() =>
            new GetUserPublicKeyResponse(identityCache);

        [PublicAPI("Used by StreamingAssets/Js/Modules/UserIdentity.js")]
        public object GetOwnUserData()
        {
            async UniTask<GetUserDataResponse> GetOwnUserDataAsync(CancellationToken ct)
            {
                try
                {
                    await UniTask.SwitchToMainThread();

                    IWeb3Identity? identity = identityCache.Identity;

                    if (identity == null)
                        return CreateGuestUserData();

                    Profile? profile = await profileRepository.GetAsync(identity.Address, ct, IProfileRepository.BatchBehaviour.ENFORCE_SINGLE_GET);

                    if (profile == null)
                        return CreateGuestUserData();

                    lock (wearablesCache)
                    {
                        wearablesCache.Clear();

                        foreach (URN urn in profile.Avatar.Wearables)
                            wearablesCache.Add(urn);

                        return new GetUserDataResponse(profile, identity, wearablesCache);
                    }
                }
                catch (Exception e)
                {
                    sceneExceptionsHandler.OnEngineException(e);
                    return CreateGuestUserData();
                }
            }

            return GetOwnUserDataAsync(disposeCts.Token).ContinueWith(JsonUtility.ToJson).ToDisconnectedPromise(this);
        }

        /// <summary>
        /// Creates fake guest user data for when no identity is available (e.g., WebGL without login)
        /// </summary>
        private static GetUserDataResponse CreateGuestUserData()
        {
            var guestAvatar = new GetUserDataResponse.Data.Avatar(
                bodyShape: "urn:decentraland:off-chain:base-avatars:BaseMale",
                eyeColor: "#704232",
                hairColor: "#000000",
                skinColor: "#CC9B76",
                wearables: new List<string>
                {
                    "urn:decentraland:off-chain:base-avatars:casual_hair_01",
                    "urn:decentraland:off-chain:base-avatars:eyes_00",
                    "urn:decentraland:off-chain:base-avatars:eyebrows_00",
                    "urn:decentraland:off-chain:base-avatars:mouth_00",
                    "urn:decentraland:off-chain:base-avatars:green_hoodie",
                    "urn:decentraland:off-chain:base-avatars:brown_pants",
                    "urn:decentraland:off-chain:base-avatars:sneakers"
                },
                snapshots: new GetUserDataResponse.Data.Avatar.Snapshot("", "")
            );

            var guestData = new GetUserDataResponse.Data(
                displayName: "Guest",
                publicKey: "0x0000000000000000000000000000000000000000",
                hasConnectedWeb3: false,
                userId: "guest-user",
                version: 1,
                avatar: guestAvatar
            );

            return new GetUserDataResponse(guestData);
        }
    }
}
