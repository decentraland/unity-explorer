using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile
    {
        UniTask<Profile?> ProfileAsync(CancellationToken ct);
        UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true);
        UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct, bool updateAvatarInWorld = true);
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
                await selfProfile.UpdateProfileAsync(ct,
                    // Don't attempt to modify the avatar, this is a generic call so we don't want to involve extra processing
                    updateAvatarInWorld: false);

            return (await selfProfile.ProfileAsync(ct)).EnsureNotNull();
        }
    }
}
