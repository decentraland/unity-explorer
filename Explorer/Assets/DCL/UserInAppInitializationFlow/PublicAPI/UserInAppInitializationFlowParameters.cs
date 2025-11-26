using Arch.Core;
using DCL.Utility.Types;

namespace DCL.UserInAppInitializationFlow
{
    public readonly struct UserInAppInitializationFlowParameters
    {
        public bool ShowAuthentication { get; }
        public bool ShowLoading { get; }
        public IUserInAppInitializationFlow.LoadSource LoadSource { get; }
        public EnumResult<TaskError> RecoveryError { get; }
        public World World { get; }
        public Entity PlayerEntity { get; }

        public UserInAppInitializationFlowParameters(
            bool showAuthentication,
            bool showLoading,
            IUserInAppInitializationFlow.LoadSource loadSource,
            World world,
            Entity playerEntity,
            EnumResult<TaskError> recoveryError = default
        )
        {
            ShowAuthentication = showAuthentication;
            ShowLoading = showLoading;
            LoadSource = loadSource;
            World = world;
            PlayerEntity = playerEntity;
            RecoveryError = recoveryError;
        }
    }
}
