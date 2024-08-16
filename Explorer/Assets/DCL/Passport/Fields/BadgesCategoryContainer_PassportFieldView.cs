using UnityEngine;

namespace DCL.Passport.Fields
{
    public class BadgesCategoryContainer_PassportFieldView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform BadgeDetailCardsContainer { get; private set; }

        public string Category { get; set; }
    }
}
