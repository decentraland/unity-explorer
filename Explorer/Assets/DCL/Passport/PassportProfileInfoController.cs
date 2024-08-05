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

        private readonly ISelfProfile selfProfile;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly PassportErrorsController passportErrorsController;

        public PassportProfileInfoController(
            ISelfProfile selfProfile,
            World world,
            Entity playerEntity,
            PassportErrorsController passportErrorsController)
        {
            this.selfProfile = selfProfile;
            this.world = world;
            this.playerEntity = playerEntity;
            this.passportErrorsController = passportErrorsController;
        }

        public async UniTask UpdateProfileAsync(CancellationToken ct)
        {
            try
            {
                // Update profile data
                var profile = await selfProfile.ForcePublishWithoutModificationsAsync(ct);

                if (profile != null)
                {
                    // Update player entity in world
                    profile.IsDirty = true;
                    world.Set(playerEntity, profile);

                    OnProfilePublished?.Invoke(profile);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to update your profile info. Please try again!";
                passportErrorsController.Show();
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
        }
    }
}
