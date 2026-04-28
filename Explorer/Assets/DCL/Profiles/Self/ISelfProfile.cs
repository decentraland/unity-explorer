using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile : IDisposable
    {
        public event Action<Profile>? ProfilePropagated;

        UniTask<Profile?> ProfileAsync(CancellationToken ct);
        UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true);
        UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct, bool updateAvatarInWorld = true);
    }
}
