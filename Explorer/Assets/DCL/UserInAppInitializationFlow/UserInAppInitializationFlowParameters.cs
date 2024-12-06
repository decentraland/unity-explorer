using Arch.Core;

namespace DCL.UserInAppInitializationFlow
{
    public readonly struct UserInAppInitializationFlowParameters
    {
        public bool ShowAuthentication { get; }
        public bool ShowLoading { get; }
        public bool ReloadRealm { get; }
        public bool FromLogout { get; }
        public World World { get; }
        public Entity PlayerEntity { get; }

        public UserInAppInitializationFlowParameters(
            bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            bool fromLogout,
            World world,
            Entity playerEntity
        )
        {
            ShowAuthentication = showAuthentication;
            ShowLoading = showLoading;
            ReloadRealm = reloadRealm;
            FromLogout = fromLogout;
            World = world;
            PlayerEntity = playerEntity;
        }
    }
}
