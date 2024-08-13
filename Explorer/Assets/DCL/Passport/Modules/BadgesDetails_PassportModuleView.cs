using DCL.Passport.Fields;
using DCL.UI;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class BadgesDetails_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public ButtonWithSelectableStateView BadgesFilterButtonPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform BadgesFilterButtonsContainer { get; private set; }

        [field: SerializeField]
        public BadgeDetailCard_PassportFieldView BadgeDetailCardPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform BadgeDetailCardsContainer { get; private set; }
    }
}
