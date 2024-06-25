using DCL.Profiles;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class EquippedItems_PassportModuleController : IPassportModuleController
    {
        private readonly EquippedItems_PassportModuleView view;

        private Profile currentProfile;

        public EquippedItems_PassportModuleController(EquippedItems_PassportModuleView view)
        {
            this.view = view;
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            // TODO: Implement this method...

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.EquippedItemsContainer);
        }

        public void Clear()
        {

        }

        public void Dispose() =>
            Clear();
    }
}
