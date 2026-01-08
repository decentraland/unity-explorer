using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
using System;
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
}
