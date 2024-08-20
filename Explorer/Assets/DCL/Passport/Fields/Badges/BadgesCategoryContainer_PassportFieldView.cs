using UnityEngine;

namespace DCL.Passport.Fields.Badges
{
    public class BadgesCategoryContainer_PassportFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform BadgeDetailCardsContainer { get; private set; }

        public string Category { get; set; }
    }
}
