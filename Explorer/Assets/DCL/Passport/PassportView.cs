using DCL.CharacterPreview;
using DCL.Passport.Modules;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport
{
    public class PassportView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public ScrollRect MainScroll { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public Image BackgroundImage { get; private set; }

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; }

        [field: SerializeField]
        public UserBasicInfo_PassportModuleView UserBasicInfoModuleView { get; private set; }

        [field: SerializeField]
        public UserDetailedInfo_PassportModuleView UserDetailedInfoModuleView { get; private set; }

        [field: SerializeField]
        public EquippedItems_PassportModuleView EquippedItemsModuleView { get; private set; }
    }
}
