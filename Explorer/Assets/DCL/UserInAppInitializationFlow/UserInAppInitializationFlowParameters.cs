using Arch.Core;

namespace DCL.UserInAppInitializationFlow
{
    public readonly struct UserInAppInitializationFlowParameters
    {
        public bool ShowAuthentication { get; }
        public bool ShowLoading { get; }
        public bool ReloadRealm { get; }
        public IUserInAppInitializationFlow.LoadSource LoadSource { get; }
        public string? RecoveryErrorMessage { get; }
        public World World { get; }
        public Entity PlayerEntity { get; }

        public UserInAppInitializationFlowParameters(
            bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            IUserInAppInitializationFlow.LoadSource loadSource,
            World world,
            Entity playerEntity,
            string? recoveryErrorMessage = null
        )
        {
            ShowAuthentication = showAuthentication;
            ShowLoading = showLoading;
            ReloadRealm = reloadRealm;
            LoadSource = loadSource;
            World = world;
            PlayerEntity = playerEntity;
            RecoveryErrorMessage = recoveryErrorMessage;
        }
    }
}
