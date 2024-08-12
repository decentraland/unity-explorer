using DCL.Passport.Fields;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class BadgesOverview_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public BadgeOverviewItem_PassportFieldView BadgeOverviewItemPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform BadgeOverviewItemsContainer { get; private set; }

        [field: SerializeField]
        public GameObject NoBadgesLabel { get; private set; }
    }
}
