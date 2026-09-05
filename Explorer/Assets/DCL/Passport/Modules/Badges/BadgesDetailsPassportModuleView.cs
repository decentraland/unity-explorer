using DCL.Passport.Fields.Badges;
using DCL.UI;
using UnityEngine;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesDetailsPassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public ButtonWithSelectableStateView BadgesFilterButtonPrefab { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform BadgesFilterButtonsContainer { get; private set; } = null!;

        [field: SerializeField]
        public BadgesCategorySeparator_PassportFieldView BadgesCategorySeparatorPrefab { get; private set; } = null!;

        [field: SerializeField]
        public BadgesCategoryContainer_PassportFieldView BadgesCategoryContainerPrefab { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform MainContainer { get; private set; } = null!;

        [field: SerializeField]
        public BadgeDetailCard_PassportFieldView BadgeDetailCardPrefab { get; private set; } = null!;

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; } = null!;

        [field: SerializeField]
        public GameObject NoBadgesLabel { get; private set; } = null!;
    }
}
