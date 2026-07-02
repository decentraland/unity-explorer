using DCL.Passport.Fields.Badges;
using UnityEngine;

namespace DCL.Passport.Modules.Badges
{
    public class BadgesOverview_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public BadgeOverviewItem_PassportFieldView BadgeOverviewItemPrefab { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform BadgeOverviewItemsContainer { get; private set; } = null!;

        [field: SerializeField]
        public GameObject NoBadgesLabel { get; private set; } = null!;
    }
}
