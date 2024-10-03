using Arch.Core;

namespace DCL.UserInAppInitializationFlow
{
    public class UserInAppInitializationFlowParameters
    {
        public bool ShowAuthentication { get; set; }
        public bool ShowLoading { get; set; }
        public bool ReloadRealm { get; set; }
        public bool FromLogout { get; set; }
        public World World { get; set; }
        public Entity PlayerEntity { get; set; }

        public UserInAppInitializationFlowParameters(
            bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            bool fromLogout,
            World world,
            Entity playerEntity)
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
