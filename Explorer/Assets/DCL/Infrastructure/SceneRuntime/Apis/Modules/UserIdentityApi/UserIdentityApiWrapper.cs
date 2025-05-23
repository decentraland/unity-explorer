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
                        return new GetUserDataResponse(null);

                    Profile? profile = await profileRepository.GetAsync(identity.Address, ct);

                    if (profile == null)
                        return new GetUserDataResponse(null);

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
                    return new GetUserDataResponse(null);
                }
            }

            return GetOwnUserDataAsync(disposeCts.Token).ContinueWith(JsonUtility.ToJson).ToDisconnectedPromise(this);
        }
    }
}
