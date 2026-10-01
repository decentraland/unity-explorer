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

        private readonly SelfProfile selfProfile;

        public PassportProfileInfoController(SelfProfile selfProfile)
        {
            this.selfProfile = selfProfile;
        }

        public async UniTask UpdateProfileAsync(Profile profile, CancellationToken ct)
        {
            ProfileDeployResult deploy = await selfProfile.DeployProfileAsync(profile, ct);

            if (deploy.IsOk(out Profile? updatedProfile))
            {
                OnProfilePublished?.Invoke(updatedProfile);
                return;
            }

            if (deploy.IsError(out ProfileDeployError error) && error is ProfileDeployError.DeployFailed or ProfileDeployError.NoIdentity)
            {
                const string ERROR_MESSAGE = "There was an error while trying to update your profile info. Please try again!";
                PublishError?.Invoke();
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {error}");
            }
        }
    }
}
