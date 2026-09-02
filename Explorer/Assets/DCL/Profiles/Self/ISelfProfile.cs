using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile : IDisposable
    {
        public event Action<Profile>? ProfilePropagated;

        /// <summary>
        ///     The own profile resolved from the cache. Can be null if the profile hasn't been fetched yet.
        /// </summary>
        Profile? OwnProfile { get; }

        UniTask<Profile?> ProfileAsync(CancellationToken ct);
        UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true);
        UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct, bool updateAvatarInWorld = true);
    }
}
