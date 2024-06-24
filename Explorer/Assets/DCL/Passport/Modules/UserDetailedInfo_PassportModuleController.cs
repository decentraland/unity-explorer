using DCL.Profiles;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IPassportModuleController
    {
        private UserDetailedInfo_PassportModuleView view;
        private Profile currentProfile;

        public UserDetailedInfo_PassportModuleController(UserDetailedInfo_PassportModuleView view)
        {
            this.view = view;
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            view.Description.text = !string.IsNullOrEmpty(profile.Description) ? profile.Description : "No description available.";

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
        }

        public void Dispose()
        {

        }
    }
}
