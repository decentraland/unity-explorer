using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles
{
    public interface IProfileRepository
    {
        public enum BatchBehaviour : byte
        {
            /// <summary>
            ///     Individual requests will be delayed and batched together
            /// </summary>
            DEFAULT,

            /// <summary>
            ///     Individual request will be dispatched immediately
            ///     Use it with caution to enforce a single request when "foreground" strictly requires only one profile
            /// </summary>
            ENFORCE_SINGLE_GET,
        }

        public const string GUEST_RANDOM_ID = "fakeUserId";
        public const string PLAYER_RANDOM_ID = "Player";

        public UniTask SetAsync(Profile profile, CancellationToken ct);

        /// <summary>
        ///     The request won't be delayed until all profiles are retrieved or attempts are exhausted <br />
        ///     The request won't throw any exceptions <br />
        ///     Instead, it can return an incomplete list <br />
        /// </summary>
        public UniTask<List<Profile>> GetAsync(IReadOnlyList<string> ids, CancellationToken ct, URLDomain? fromCatalyst = null);

        /// <returns>
        ///     'Null' if Not Found; <br />
        ///     Logs and re-throws all exceptions. They should be caught on the caller side if needed. Logging on the caller side is optional; <br />
        ///     The execution can be considerably delayed due to the Catalyst replication according to "CatalystRetryPolicy"
        /// </returns>
        public UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool getFromCacheIfPossible = true,
            BatchBehaviour batchBehaviour = BatchBehaviour.DEFAULT,
            IPartitionComponent? partition = null,
            RetryPolicy? retryPolicy = null);
    }

    public static class ProfileRepositoryExtensions
    {
        /// <summary>
        ///     Suppresses inner exceptions to 'Null' return value
        /// </summary>
        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, CancellationToken ct, IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT, RetryPolicy? retryPolicy = null) =>
            profileRepository.GetAsync(id, 0, null, ct, batchBehaviour: batchBehaviour, retryPolicy: retryPolicy).SuppressAnyExceptionWithFallback(null);

        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, int version, CancellationToken ct, bool getFromCacheIfPossible = true,
            IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT) =>
            profileRepository.GetAsync(id, version, null, ct, getFromCacheIfPossible, batchBehaviour);
    }
}
