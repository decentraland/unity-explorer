using Arch.Core;

namespace DCL.UserInAppInitializationFlow
{
    public struct UserInAppInitializationFlowParameters
    {
        public bool ShowAuthentication { get; set; }
        public bool ShowLoading { get; set; }
        public bool ReloadRealm { get; set; }
        public bool FromLogout { get; set; }
        public World World { get; set; }
        public Entity PlayerEntity { get; set; }
    }
}
