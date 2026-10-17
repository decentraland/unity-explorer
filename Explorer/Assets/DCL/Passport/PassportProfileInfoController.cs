using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using System;
using System.Threading;

namespace DCL.Passport
{
    public class PassportProfileInfoController
    {
        public event Action<Profile>? OnProfilePublished;
        public event Action? PublishError;

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

            if (deploy.IsFailure(out _))
                PublishError?.Invoke();
        }
    }
}
