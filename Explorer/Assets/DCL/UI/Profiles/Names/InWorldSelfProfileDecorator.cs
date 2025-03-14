using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using System.Threading;

namespace DCL.UI.ProfileNames
{
    /// <summary>
    /// Whenever the profile is published, its also is updated in the ECS state, so it is updated in-world
    /// </summary>
    public class InWorldSelfProfileDecorator : ISelfProfile
    {
        private readonly ISelfProfile origin;
        private readonly World world;
        private readonly Entity playerEntity;

        public InWorldSelfProfileDecorator(ISelfProfile origin,
            World world,
            Entity playerEntity)
        {
            this.origin = origin;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public UniTask<Profile?> ProfileAsync(CancellationToken ct) =>
            origin.ProfileAsync(ct);

        public async UniTask<Profile?> UpdateProfileAsync(bool publish, CancellationToken ct)
        {
            Profile? profile = await origin.UpdateProfileAsync(publish, ct);

            if (profile != null)
                UpdateAvatarInWorld(profile);

            return profile;
        }

        public async UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct)
        {
            Profile? newProfile = await origin.UpdateProfileAsync(profile, ct);

            if (newProfile != null)
                UpdateAvatarInWorld(newProfile);

            return newProfile;
        }

        public UniTask<Profile?> ForcePublishWithoutModificationsAsync(CancellationToken ct) =>
            // What is the point of this call? It feels odd to publish the profile without modifications..
            origin.ForcePublishWithoutModificationsAsync(ct);

        private void UpdateAvatarInWorld(Profile profile)
        {
            profile.IsDirty = true;

            bool found = world.Has<Profile>(playerEntity);

            if (found)
                world.Set(playerEntity, profile);
            else
                world.Add(playerEntity, profile);
        }
    }
}
