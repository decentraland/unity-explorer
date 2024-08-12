using UnityEngine;

namespace DCL.Passport.Modules
{
    public class BadgesDetails_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public BadgeDetailCard_PassportFieldView BadgeDetailCardPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform BadgeDetailCardsContainer { get; private set; }
    }
}
