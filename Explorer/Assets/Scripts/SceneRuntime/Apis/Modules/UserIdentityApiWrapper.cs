using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace SceneRuntime.Apis.Modules
{
    public partial class UserIdentityApiWrapper : IDisposable
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly List<string> wearablesCache = new ();
        private readonly CancellationTokenSource lifeCycleCts = new ();

        public UserIdentityApiWrapper(IProfileRepository profileRepository,
            IWeb3IdentityCache identityCache,
            ISceneExceptionsHandler sceneExceptionsHandler)
        {
            this.profileRepository = profileRepository;
            this.identityCache = identityCache;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        public void Dispose()
        {
            lifeCycleCts.SafeCancelAndDispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/UserIdentity.js")]
        public object GetOwnUserData()
        {
            async UniTask<GetUserDataResponse.Data?> GetOwnUserDataAsync(CancellationToken ct)
            {
                try
                {
                    IWeb3Identity identity = identityCache.Identity!;
                    Profile? profile = await profileRepository.GetAsync(identity.Address, 0, ct);

                    if (profile == null)
                        return null;

                    Avatar avatar = profile.Avatar;

                    lock (wearablesCache)
                    {
                        wearablesCache.Clear();

                        foreach (URN urn in avatar.Wearables)
                            wearablesCache.Add(urn);

                        return new GetUserDataResponse.Data
                        {
                            version = profile.Version,
                            displayName = profile.DisplayName,
                            publicKey = identity.Address,
                            hasConnectedWeb3 = profile.HasConnectedWeb3,
                            userId = profile.UserId,
                            avatar = new GetUserDataResponse.Data.Avatar
                            {
                                snapshots = new GetUserDataResponse.Data.Avatar.Snapshot
                                {
                                    body = avatar.BodySnapshotUrl,
                                    face256 = avatar.FaceSnapshotUrl,
                                },
                                wearables = wearablesCache,
                                bodyShape = avatar.BodyShape,
                                eyeColor = $"#{ColorUtility.ToHtmlStringRGB(avatar.EyesColor)}",
                                hairColor = $"#{ColorUtility.ToHtmlStringRGB(avatar.HairColor)}",
                                skinColor = $"#{ColorUtility.ToHtmlStringRGB(avatar.SkinColor)}",
                            },
                        };
                    }
                }
                catch (Exception e)
                {
                    sceneExceptionsHandler.OnEngineException(e);

                    return null;
                }
            }

            return GetOwnUserDataAsync(lifeCycleCts.Token)
                  .ContinueWith(ToUserDataResponse)
                  .AsTask()
                  .ToPromise();
        }

        private static GetUserDataResponse ToUserDataResponse(GetUserDataResponse.Data? data)
        {
            var response = new GetUserDataResponse { data = data };
            return response;
        }
    }
}
