using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using System;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile : IDisposable
    {
        UniTask<Profile?> ProfileAsync(CancellationToken ct);
        UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true);
        UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct, bool updateAvatarInWorld = true);
    }
}
