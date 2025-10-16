using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using System.Threading;

namespace DCL.Profiles
{
    public interface IProfileRepository
    {
        public const string GUEST_RANDOM_ID = "fakeUserId";
        public const string PLAYER_RANDOM_ID = "Player";

        UniTask SetAsync(Profile profile, CancellationToken ct);

        /// <returns>
        ///     'Null' if Not Found; <br />
        ///     Logs and re-throws all exceptions. They should be caught on the caller side if needed. Logging on the caller side is optional; <br />
        /// </returns>
        UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool getFromCacheIfPossible = true);
    }

    public static class ProfileRepositoryExtensions
    {
        /// <summary>
        ///     Suppresses inner exceptions to 'Null' return value
        /// </summary>
        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, CancellationToken ct) =>
            profileRepository.GetAsync(id, 0, null, ct).SuppressAnyExceptionWithFallback(null);

        public static UniTask<Profile?> GetAsync(this IProfileRepository profileRepository, string id, int version, CancellationToken ct, bool getFromCacheIfPossible = true) =>
            profileRepository.GetAsync(id, version, null, ct, getFromCacheIfPossible);
    }
}
