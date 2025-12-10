using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using IpfsProfileEntity = DCL.Ipfs.EntityDefinitionGeneric<DCL.Profiles.GetProfileJsonRootDto>;

namespace DCL.Profiles
{
    public partial class RealmProfileRepository : IBatchedProfileRepository
    {
        public static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new () { Converters = new JsonConverter[] { new ProfileJsonRootDtoConverter() } };

        private readonly int batchMaxSize;

        private readonly IWebRequestController webRequestController;
        private readonly ProfilesDebug profilesDebug;
        private readonly IRealmData realm;
        private readonly IProfileCache profileCache;
        private readonly URLBuilder urlBuilder = new ();
        private readonly Dictionary<string, byte[]> files = new ();

        /// <summary>
        ///     It's a simple list, not a dictionary because the number of different lambdas is very limited
        /// </summary>
        private readonly List<ProfilesBatchRequest> pendingBatches = new (10);

        private readonly List<ProfilesBatchRequest> ongoingBatches = new (10);

        // private readonly Dictionary<string, UniTaskCompletionSource> ongoingRequests = new (PoolConstants.AVATARS_COUNT);

        // Catalyst servers requires a face thumbnail texture of 256x256
        // Otherwise it will fail when the profile is published
        private readonly byte[] whiteTexturePng = new Texture2D(256, 256).EncodeToPNG();

        public RealmProfileRepository(
            IWebRequestController webRequestController,
            IRealmData realm,
            IProfileCache profileCache,
            ProfilesDebug profilesDebug)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
            this.profileCache = profileCache;
            this.profilesDebug = profilesDebug;
        }

        public IEnumerable<ProfilesBatchRequest> ConsumePendingBatch()
        {
            lock (pendingBatches)
            lock (ongoingBatches)
            {
                int count = pendingBatches.Count;
                if (count == 0) return Enumerable.Empty<ProfilesBatchRequest>();

                ongoingBatches.AddRange(pendingBatches);
                pendingBatches.Clear();
                return ongoingBatches.TakeLast(count);
            }
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
                content = new ContentDefinition[]
                {
                    new () { file = "body.png", hash = bodyHash },
                    new () { file = "face256.png", hash = faceHash },
                },
                pointers = new[] { profile.UserId },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = IpfsRealmEntityType.Profile.ToEntityString(),
            };

        private bool TryGetExistingRequest(string userId, out ProfilesBatchRequest.Input req) =>
            TryGetExistingRequest(userId, ongoingBatches, out req) || TryGetExistingRequest(userId, pendingBatches, out req);

        private bool TryGetExistingRequest(string userId, List<ProfilesBatchRequest> list, out ProfilesBatchRequest.Input req)
        {
            lock (list)
            {
                foreach (ProfilesBatchRequest profilesBatch in list)
                {
                    if (profilesBatch.PendingRequests.TryGetValue(userId, out req))
                        return true;
                }

                req = default(ProfilesBatchRequest.Input);
                return false;
            }
        }

        private void Resolve(string userId, Profile? profile)
        {
            if (TryRemoveOngoingRequest(userId, out UniTaskCompletionSource<Profile?> continuation))
                continuation.TrySetResult(profile);
        }

        private void ResolveException(string userId, Exception ex)
        {
            if (TryRemoveOngoingRequest(userId, out UniTaskCompletionSource<Profile?> continuation))
                continuation.TrySetException(ex);
        }

        private bool TryRemoveOngoingRequest(string userId, out UniTaskCompletionSource<Profile?> ongoingRequest)
        {
            lock (ongoingBatches)
            {
                for (int i = 0; i < ongoingBatches.Count; i++)
                {
                    ProfilesBatchRequest profilesBatch = ongoingBatches[i];

                    if (profilesBatch.PendingRequests.Remove(userId, out ProfilesBatchRequest.Input req))
                    {
                        ongoingRequest = req.Cs;

                        if (profilesBatch.PendingRequests.Count == 0)
                        {
                            profilesBatch.Dispose();
                            ongoingBatches.RemoveAtSwapBack(i);
                            --i;
                        }

                        return true;
                    }
                }
            }

            ongoingRequest = null;
            return false;
        }

        private UniTaskCompletionSource<Profile?> AddToBatch(string userId, URLDomain? fromCatalyst,
            List<ProfilesBatchRequest> requests, IPartitionComponent partition)
        {
            fromCatalyst ??= realm.Ipfs.LambdasBaseUrl;

            ProfilesBatchRequest? batch = null;

            lock (requests)
            {
                foreach (ProfilesBatchRequest profilesBatch in requests)
                {
                    if (profilesBatch.LambdasUrl.Value == fromCatalyst.Value.Value)
                    {
                        batch = profilesBatch;
                        break;
                    }
                }

                if (batch == null)
                    requests.Add((batch = ProfilesBatchRequest.Create(fromCatalyst.Value)).Value);

                if (batch.Value.PendingRequests.TryGetValue(userId, out ProfilesBatchRequest.Input request))
                    return request.Cs;

                var cs = new UniTaskCompletionSource<Profile?>();

                batch.Value.PendingRequests.Add(userId, new ProfilesBatchRequest.Input(cs, partition));
                return cs;
            }
        }

        public async UniTask<List<Profile>> GetAsync(IReadOnlyList<string> ids, CancellationToken ct, URLDomain? fromCatalyst = null)
        {
            // Tolerate or fix this allocation?
            var results = new List<Profile>(ids.Count);

            // Pool is inside WhenAll
            await UniTask.WhenAll(ids.Select(WaitForProfileAsync));

            async UniTask WaitForProfileAsync(string id)
            {
                Result<Profile?> profile = await GetAsync(id, 0, fromCatalyst, ct, false, true)
                   .SuppressToResultAsync();

                if (profile is { Success: true, Value: not null })
                    results.Add(profile.Value);
            }

            return results;
        }

        private async UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct,
            bool delayBatchResolution,
            bool getFromCacheIfPossible = true,
            IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT,
            IPartitionComponent? partition = null,
            RetryPolicy? retryPolicy = null)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return null;

                // if there is an ongoing request wait for it, as it will override the value from the cache (return it to the pool)
                // that makes the object empty (unusable)

                // Even if it's a single request, but it's already in a batch wait for it

                while (TryGetExistingRequest(id, out ProfilesBatchRequest.Input request))
                    await request.Cs.Task.AttachExternalCancellation(ct);

                if (getFromCacheIfPossible)
                    if (TryProfileFromCache(id, version, out Profile? profileInCache))
                        return profileInCache;

                partition ??= PartitionComponent.TOP_PRIORITY;

                // Two paths
                switch (batchBehaviour)
                {
                    case IProfileRepository.BatchBehaviour.DEFAULT:
                        // Batching is allowed
                        Profile? result = await AddToBatch(id, fromCatalyst, pendingBatches, partition).Task!.AttachExternalCancellation<Profile>(ct);

                        // Not found profiles (Catalyst replication delays) will be processed individually by GET
                        // ⚠️ The following produces potentially a very long-living task ⚠️
                        // ⚠️ One request gives birth to many => potential distribution error, but it's a workaround for catalysts anyway ⚠️
                        return result == null && delayBatchResolution ? await EnforceSingleGetAsync(retryPolicy) : result;

                    case IProfileRepository.BatchBehaviour.ENFORCE_SINGLE_GET:
                        return await EnforceSingleGetAsync(retryPolicy);
                    default:
                        throw new NotSupportedException($"BatchBehaviour {batchBehaviour} not supported");
                }
            }
            finally { await UniTask.SwitchToMainThread(); }

            UniTask<Profile?> EnforceSingleGetAsync(RetryPolicy? retryPolicy)
            {
                // Add directly to the ongoing batch as it's dispatch immediately
                // It's needed if the same profile is requested again (Single or in the batch) so it will wait for the existing request
                AddToBatch(id, fromCatalyst, ongoingBatches, partition);

                // Launch single request
                // It still can return `null` if all atempts are exhausted
                return ExecuteSingleGetAsync(id, fromCatalyst, retryPolicy, ct);
            }
        }

        public UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool getFromCacheIfPossible = true,
            IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT,
            IPartitionComponent? partition = null,
            RetryPolicy? retryPolicy = null) =>
            GetAsync(id, version, fromCatalyst, ct, true, getFromCacheIfPossible, batchBehaviour, partition, retryPolicy);

        /// <summary>
        ///     Should be called from the background thread
        /// </summary>
        public Profile? ResolveProfile(string userId, ProfileJsonDto? profileDTO)
        {
            Profile? profile = null;

            if (profileDTO != null)
            {
                // Reusing the profile in cache does not allow other systems to properly update.
                // It impacts on the object state and does not allow to make comparisons on change.
                // For example the multiplayer system, whenever a remote profile update comes in,
                // it compares the version of the profile to check if it has changed. By overriding the version here,
                // the check always fails. So its necessary to get a new instance each time
                profile = Profile.Create();
                profileDTO.CopyTo(profile);
                profile.UserNameColor = NameColorHelper.GetNameColor(profile.DisplayName);

                profileCache.Set(userId, profile);
            }
            else
                profilesDebug.AddMissing(userId);

            // Find the request in the batch
            Resolve(userId, profile);

            return profile;
        }

        /// <summary>
        ///     Enforces single get without prioritization and batching
        /// </summary>
        /// <returns></returns>
        private async UniTask<Profile?> ExecuteSingleGetAsync(string id, URLDomain? fromCatalyst, RetryPolicy? retryPolicy, CancellationToken ct)
        {
            try
            {
                URLAddress url = GetUrl(id, fromCatalyst);

                // Suppress logging errors here as we have very custom errors handling below
                GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> response = webRequestController.GetAsync(
                    new CommonArguments(url, retryPolicy ?? CatalystRetryPolicy.VALUE),
                    ct,
                    ReportCategory.PROFILE,
                    suppressErrors: true);

                using GetProfileJsonRootDto? root = await response.CreateFromNewtonsoftJsonAsync<GetProfileJsonRootDto>(
                    createCustomExceptionOnFailure: (exception, text) => new ProfileParseException(id, text, exception),
                    serializerSettings: SERIALIZER_SETTINGS);

                ProfileJsonDto? profileDto = root?.FirstProfileDto();

                profilesDebug.AddNonAggregated();

                return ResolveProfile(id, profileDto);
            }
            catch (UnityWebRequestException e) when (e is { ResponseCode: WebRequestUtils.NOT_FOUND })
            {
                ReportProfileNotFound(id, fromCatalyst);

                ResolveException(id, e);

                return null;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Report other exceptions normally
                ReportHub.LogException(e, ReportCategory.PROFILE);

                ResolveException(id, e);

                throw;
            }
            catch (Exception e)
            {
                ResolveException(id, e);
                throw;
            }
        }

        private static void ReportProfileNotFound(string id, URLDomain? fromCatalyst) =>

            // Report to console every time
            // Report to Sentry only once per session (protect from spamming)
            ReportHub.LogError(new ReportData(ReportCategory.PROFILE, new ReportDebounce(SentryStaticDebouncer.Instance)),
                $"Profile not found, WalletId: {id})\nFrom Catalyst: {fromCatalyst}");

        private URLAddress GetUrl(string id, URLDomain? fromCatalyst)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(fromCatalyst ?? realm.Ipfs.LambdasBaseUrl)
                      .AppendPath(URLPath.FromString($"profiles/{id}"));

            return urlBuilder.Build();
        }

        public URLAddress PostUrl(URLDomain fromCatalyst)
        {
            urlBuilder.Clear();

            // Warning: putting a trailing slash break the backend routing!
            urlBuilder.AppendDomain(fromCatalyst)
                      .AppendPath(URLPath.FromString("profiles"));

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
