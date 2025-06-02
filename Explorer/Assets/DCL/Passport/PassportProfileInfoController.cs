using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using System;
using System.Threading;

namespace DCL.Passport
{
    public class PassportProfileInfoController
    {
        public event Action<Profile>? OnProfilePublished;
        public event Action PublishError;

        private readonly ISelfProfile selfProfile;
        private readonly World world;
        private readonly Entity playerEntity;

        public PassportProfileInfoController(
            ISelfProfile selfProfile,
            World world,
            Entity playerEntity)
        {
            this.selfProfile = selfProfile;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask UpdateProfileAsync(Profile profile, CancellationToken ct)
        {
            try
            {
                // Update profile data
                var updatedProfile = await selfProfile.UpdateProfileAsync(profile, ct);

                if (updatedProfile != null)
                {
                    // Update player entity in world
                    updatedProfile.IsDirty = true;
                    world.Set(playerEntity, updatedProfile);

                    OnProfilePublished?.Invoke(updatedProfile);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to update your profile info. Please try again!";
                PublishError?.Invoke();
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
