using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using System.Threading;

namespace DCL.Profiles.Self
{
    public interface ISelfProfile
    {
        UniTask<Profile?> ProfileAsync(CancellationToken ct);

        UniTask<Profile?> UpdateProfileAsync(CancellationToken ct);
        UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct);

        /// <summary>
        /// It only updates the basic info of the profile and ignore the rest of the data.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The published profile.</returns>
        /// TODO: it is odd in the interface design. Why would you want to publish the profile without modifications? Consider redesign..
        UniTask<Profile?> ForcePublishWithoutModificationsAsync(CancellationToken ct);
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
                await selfProfile.UpdateProfileAsync(ct);

            return (await selfProfile.ProfileAsync(ct)).EnsureNotNull();
        }
    }
}
