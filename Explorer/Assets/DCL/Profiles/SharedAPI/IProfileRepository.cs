using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles
{
    public interface IProfileRepository
    {
        [Flags]
        public enum FetchBehaviour : byte
        {
            /// <summary>
            ///     Individual requests will be delayed and batched together
            /// </summary>
            DEFAULT = 0,

            /// <summary>
            ///     Individual request will be dispatched immediately
            ///     Use it with caution to enforce a single request when "foreground" strictly requires only one profile
            /// </summary>
            ENFORCE_SINGLE_GET = 1,

            /// <summary>
            ///     The request can be delayed according to the retry policy until the required version or non-null profile is provided <br />
            ///     The request can still return `null` as a last measure (if all attempts exceeded) <br />
            ///     When the request is not resolved it will be repeated in a non-batched mode
            /// </summary>
            DELAY_UNTIL_RESOLVED = 2,
        }

        public const string PROFILE_FRAGMENTATION_OBSOLESCENCE = "Should be moved to the unified POST originated from the client";

        public const string GUEST_RANDOM_ID = "fakeUserId";
        public const string PLAYER_RANDOM_ID = "Player";

        public UniTask SetAsync(Profile profile, CancellationToken ct);

        public UniTask<ProfileTier?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct,
            bool getFromCacheIfPossible,
            FetchBehaviour fetchBehaviour,
            ProfileTier.Kind tier,
            IPartitionComponent? partition = null);
    }

    public static class ProfileRepositoryExtensions
    {
        /// <returns>
        ///     'Null' if Not Found; <br />
        ///     Logs and re-throws all exceptions. They should be caught on the caller side if needed. Logging on the caller side is optional; <br />
        ///     The execution can be considerably delayed due to the Catalyst replication according to "CatalystRetryPolicy"
        /// </returns>
        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, int version, URLDomain? fromCatalyst, CancellationToken ct,
            bool getFromCacheIfPossible = true,
            IProfileRepository.FetchBehaviour batchBehaviour = IProfileRepository.FetchBehaviour.DEFAULT,
            IPartitionComponent? partition = null) =>
            profileRepository.GetAsync(id, version, fromCatalyst, ct, getFromCacheIfPossible, batchBehaviour, ProfileTier.Kind.Full, partition)
                             .ContinueWith(static pt => pt.ToProfile());

        /// <summary>
        ///     Compact info is requested without connection to a catalyst or to a specific version, and always with a default batching behaviour
        /// </summary>
        public static UniTask<Profile.CompactInfo?> GetCompactAsync(this IProfileRepository profileRepository, string id, CancellationToken ct, IPartitionComponent? partition = null) =>
            profileRepository.GetAsync(id, 0, null, ct, true, IProfileRepository.FetchBehaviour.DEFAULT, ProfileTier.Kind.Compact, partition: partition)
                             .ContinueWith(static pt => pt.ToCompact());

        /// <summary>
        ///     The request won't be delayed until all profiles are retrieved or attempts are exhausted <br />
        ///     The request won't throw any exceptions <br />
        ///     Instead, it can return an incomplete list <br />
        /// </summary>
        public static async UniTask<List<Profile>> GetAsync(this IProfileRepository profileRepository, IReadOnlyList<string> ids, CancellationToken ct, URLDomain? fromCatalyst = null)
        {
            // Tolerate or fix this allocation?
            var results = new List<Profile>(ids.Count);

            // Pool is inside WhenAll
            await UniTask.WhenAll(ids.Select(WaitForProfileAsync));

            async UniTask WaitForProfileAsync(string id)
            {
                Result<ProfileTier?> profile = await profileRepository.GetAsync(id, 0, fromCatalyst, ct, true, IProfileRepository.FetchBehaviour.DEFAULT, ProfileTier.Kind.Full)
                                                                      .SuppressToResultAsync();

                if (profile is { Success: true, Value: not null })
                    results.Add(profile.Value.ToProfile()!);
            }

            return results;
        }

        /// <summary>
        ///     <inheritdoc cref="GetCompactAsync(string,System.Threading.CancellationToken,ECS.Prioritization.Components.IPartitionComponent?)" />
        /// </summary>
        public static async UniTask<List<Profile.CompactInfo>> GetCompactAsync(this IProfileRepository profileRepository, IReadOnlyList<string> ids, CancellationToken ct)
        {
            // Tolerate or fix this allocation?
            var results = new List<Profile.CompactInfo>(ids.Count);

            // Pool is inside WhenAll
            await UniTask.WhenAll(ids.Select(WaitForProfileAsync));

            async UniTask WaitForProfileAsync(string id)
            {
                Result<Profile.CompactInfo?> profile = await profileRepository.GetCompactAsync(id, ct)
                                                                              .SuppressToResultAsync();

                if (profile is { Success: true, Value: not null })
                    results.Add(profile.Value.Value);
            }

            return results;
        }

        /// <summary>
        ///     Suppresses inner exceptions to 'Null' return value
        /// </summary>
        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, CancellationToken ct, IProfileRepository.FetchBehaviour batchBehaviour = IProfileRepository.FetchBehaviour.DEFAULT) =>
            profileRepository.GetAsync(id, 0, null, ct, batchBehaviour: batchBehaviour).SuppressAnyExceptionWithFallback(null);

        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, int version, CancellationToken ct, bool getFromCacheIfPossible = true,
            IProfileRepository.FetchBehaviour batchBehaviour = IProfileRepository.FetchBehaviour.DEFAULT) =>
            profileRepository.GetAsync(id, version, null, ct, getFromCacheIfPossible, batchBehaviour);
    }
}
