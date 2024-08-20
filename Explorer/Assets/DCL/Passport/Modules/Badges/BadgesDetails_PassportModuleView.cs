using DCL.Passport.Fields.Badges;
using DCL.UI;
using UnityEngine;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesDetails_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public ButtonWithSelectableStateView BadgesFilterButtonPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform BadgesFilterButtonsContainer { get; private set; }

        [field: SerializeField]
        public BadgesCategorySeparator_PassportFieldView BadgesCategorySeparatorPrefab { get; private set; }

        [field: SerializeField]
        public BadgesCategoryContainer_PassportFieldView BadgesCategoryContainerPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform MainContainer { get; private set; }

        [field: SerializeField]
        public BadgeDetailCard_PassportFieldView BadgeDetailCardPrefab { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; }
    }
}
