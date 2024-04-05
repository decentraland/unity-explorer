using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile
    {
        UniTask<Profile?> ProfileAsync(CancellationToken ct);

        UniTask PublishAsync(CancellationToken ct);
    }

    public static class SelfProfileExtensions
    {
        public static async UniTask<bool> IsProfilePublishedAsync(this ISelfProfile selfProfile, CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            return profile != null;
        }
    }
}
