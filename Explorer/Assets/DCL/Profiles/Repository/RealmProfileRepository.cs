using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.Profiles.Helpers;
using DCL.WebRequests;
using ECS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using IpfsProfileEntity = DCL.Ipfs.EntityDefinitionGeneric<DCL.Profiles.GetProfileJsonRootDto>;

namespace DCL.Profiles
{
    /// <summary>
    ///     TODO: this class requires refactoring:
    ///     <list type="bullet">
    ///         <item>The requests should be batched as the endpoint supports an array of pointers</item>
    ///         <item>Currently, loading nearby profiles and from chat cause an unnecessary spike of requests that can be combined into one</item>
    ///         <item>It should be a part of the ECS to enable proper budgeting and deferring</item>
    ///         <item>Failures should be cached (as in LoadSystemBase). Not caching failures leads to spam of the missing profiles or incorrect addresses. The catalyst they are requested from should be respected due to the replication time</item>
    ///         <item>Concurrent requests to the same profile id should be properly handled</item>
    ///         <item>LoadSystemBase already supports all cases needed</item>
    ///     </list>
    /// </summary>
    public partial class RealmProfileRepository : IProfileRepository
    {
        private static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new ProfileJsonRootDtoConverter() } };

        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly IProfileCache profileCache;
        private readonly URLBuilder urlBuilder = new ();
        private readonly Dictionary<string, byte[]> files = new ();

        private readonly Dictionary<string, UniTaskCompletionSource> ongoingRequests = new (PoolConstants.AVATARS_COUNT);

        // Catalyst servers requires a face thumbnail texture of 256x256
        // Otherwise it will fail when the profile is published
        private readonly byte[] whiteTexturePng = new Texture2D(256, 256).EncodeToPNG();

        public RealmProfileRepository(
            IWebRequestController webRequestController,
            IRealmData realm,
            IProfileCache profileCache)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.profileCache = profileCache;
        }

        public async UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(profile.UserId))
                throw new ArgumentException("Can't set a profile with an empty UserId");

            IIpfsRealm ipfs = realm.Ipfs;

            // TODO: we are not sure if we will need to keep sending snapshots. In the meantime just use white textures
            byte[] faceSnapshotTextureFile = whiteTexturePng;
            byte[] bodySnapshotTextureFile = whiteTexturePng;

            string faceHash = ipfs.GetFileHash(faceSnapshotTextureFile);
            string bodyHash = ipfs.GetFileHash(bodySnapshotTextureFile);

            using GetProfileJsonRootDto profileDto = NewProfileJsonRootDto(profile, bodyHash, faceHash);
            IpfsProfileEntity entity = NewPublishProfileEntity(profile, profileDto, bodyHash, faceHash);

            files.Clear();
            files[bodyHash] = bodySnapshotTextureFile;
            files[faceHash] = faceSnapshotTextureFile;

            try
            {
                await ipfs.PublishAsync(entity, ct, files);
                profileCache.Set(profile.UserId, profile);
            }
            finally { files.Clear(); }
        }

        private static GetProfileJsonRootDto NewProfileJsonRootDto(Profile profile, string bodyHash, string faceHash)
        {
            var profileDto = GetProfileJsonRootDto.Create();
            profileDto.CopyFrom(profile);
            profileDto.avatars[0]!.avatar.snapshots.body = bodyHash;
            profileDto.avatars[0]!.avatar.snapshots.face256 = faceHash;
            return profileDto;
        }

        private static IpfsProfileEntity NewPublishProfileEntity(Profile profile, GetProfileJsonRootDto profileJsonRootDto, string bodyHash, string faceHash) =>
            new (string.Empty, profileJsonRootDto)
            {
                version = IpfsProfileEntity.DEFAULT_VERSION,
                content = new List<ContentDefinition>
                {
                    new () { file = "body.png", hash = bodyHash },
                    new () { file = "face256.png", hash = faceHash },
                },
                pointers = new List<string> { profile.UserId },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = IpfsRealmEntityType.Profile.ToEntityString(),
            };

        public async UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // if there is an ongoing request wait for it, as it will override the value from the cache (return it to the pool)
            // that makes the object empty (unusable)
            if (ongoingRequests.TryGetValue(id, out UniTaskCompletionSource ongoingTask))
                await ongoingTask.Task.AttachExternalCancellation(ct);

            if (TryProfileFromCache(id, version, out Profile? profileInCache)) return profileInCache;

            Assert.IsTrue(realm.Configured, "Can't get profile if the realm is not configured");

            ongoingTask = new UniTaskCompletionSource();
            ongoingRequests.Add(id, ongoingTask);

            try
            {
                URLAddress url = Url(id, fromCatalyst);
                GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> response = webRequestController.GetAsync(new CommonArguments(url), ct, ReportCategory.REALM, ignoreErrorCodes: IWebRequestController.IGNORE_NOT_FOUND);

                using GetProfileJsonRootDto? root = await response.CreateFromNewtonsoftJsonAsync<GetProfileJsonRootDto>(
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, version, text, exception),
                    serializerSettings: SERIALIZER_SETTINGS);

                ProfileJsonDto? profileDto = root?.FirstProfileDto();

                if (profileDto is null)
                    return null;

                // Reusing the profile in cache does not allow other systems to properly update.
                // It impacts on the object state and does not allow to make comparisons on change.
                // For example the multiplayer system, whenever a remote profile update comes in,
                // it compares the version of the profile to check if it has changed. By overriding the version here,
                // the check always fails. So its necessary to get a new instance each time
                var profile = Profile.Create();
                profileDto.CopyTo(profile);
                profile.UserNameColor = ProfileNameColorHelper.GetNameColor(profile.DisplayName);

                profileCache.Set(id, profile);

                return profile;
            }
            catch (UnityWebRequestException e)
            {
                if (e.ResponseCode == WebRequestUtils.NOT_FOUND)
                    return null;

                throw;
            }
            finally
            {
                ongoingRequests.Remove(id);
                ongoingTask.TrySetResult();
            }
        }

        private URLAddress Url(string id, URLDomain? fromCatalyst)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(fromCatalyst ?? realm.Ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"));

            return urlBuilder.Build();
        }

        private bool TryProfileFromCache(string id, int version, out Profile? profile)
        {
            if (!profileCache.TryGet(id, out profile))
                return false;

            return profile.Version >= version;
        }
    }
}
