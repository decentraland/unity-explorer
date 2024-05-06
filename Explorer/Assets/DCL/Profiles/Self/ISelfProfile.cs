using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile
    {
        UniTask<Profile?> ProfileAsync(CancellationToken ct);

        UniTask<Profile?> PublishAsync(CancellationToken ct);
    }

    public static class SelfProfileExtensions
    {
        public static async UniTask<bool> IsProfilePublishedAsync(this ISelfProfile selfProfile, CancellationToken ct)
        {
            var profile = await selfProfile.ProfileAsync(ct);
            return profile != null;
        }

        public static async UniTask<Profile> ProfileOrPublishIfNotAsync(this ISelfProfile selfProfile, CancellationToken ct)
        {
            bool isPublished = await selfProfile.IsProfilePublishedAsync(ct);

            if (isPublished == false)
                await selfProfile.PublishAsync(ct);

            return (await selfProfile.ProfileAsync(ct)).EnsureNotNull();
        }
    }
}
