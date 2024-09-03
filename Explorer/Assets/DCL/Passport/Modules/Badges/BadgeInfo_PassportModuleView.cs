using DCL.Passport.Fields.Badges;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules.Badges
{
    public class BadgeInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        [field: SerializeField]
        public GameObject ImageLoadingSpinner { get; private set; }

        [field: SerializeField]
        public RawImage Badge3DImage { get; private set; }

        [field: SerializeField]
        public Color Badge3DImageUnlockedColor { get; private set; }

        [field: SerializeField]
        public Color Badge3DImageLockedColor { get; private set; }

        [field: SerializeField]
        public Animator Badge3DAnimator { get; private set; }

        [field: SerializeField]
        public Material Badge3DMaterial { get; private set; }

        [field: SerializeField]
        public Sprite DefaultBadgeSprite { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeNameText { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDateText { get; private set; }

        [field: SerializeField]
        public TMP_Text BadgeDescriptionText { get; private set; }

        [field: SerializeField]
        public GameObject TierSection { get; private set; }

        [field: SerializeField]
        public BadgeTierButton_PassportFieldView BadgeTierButtonPrefab { get; private set; }

        [field: SerializeField]
        public RectTransform AllTiersContainer { get; private set; }

        [field: SerializeField]
        public GameObject TopTierMark { get; private set; }

        [field: SerializeField]
        public GameObject NextTierContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierValueText { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierDescriptionText { get; private set; }

        [field: SerializeField]
        public RectTransform NextTierProgressBar { get; private set; }

        [field: SerializeField]
        public GameObject NextTierProgressBarContainer { get; private set; }

        [field: SerializeField]
        public RectTransform NextTierProgressBarFill { get; private set; }

        [field: SerializeField]
        public TMP_Text NextTierProgressValueText { get; private set; }

        [field: SerializeField]
        public RectTransform SimpleBadgeProgressBar { get; private set; }

        [field: SerializeField]
        public GameObject SimpleBadgeProgressBarContainer { get; private set; }

        [field: SerializeField]
        public RectTransform SimpleBadgeProgressBarFill { get; private set; }

        [field: SerializeField]
        public TMP_Text SimpleBadgeProgressValueText { get; private set; }
    }
}
