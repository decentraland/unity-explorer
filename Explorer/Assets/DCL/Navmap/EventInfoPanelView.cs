using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class EventInfoPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public ImageView Thumbnail { get; private set; }

        [field: SerializeField]
        public GameObject LiveContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text LiveScheduleLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text LiveUserCountLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text ScheduleLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text AttendingUserCountLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text EventNameLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text HostAndPlaceLabel { get; private set; }

        [field: SerializeField]
        public TMP_Text DescriptionLabel { get; private set; }

        [field: SerializeField]
        public Button JumpInButton { get; private set; }

        [field: SerializeField]
        public Button ShareButton { get; private set; }

        [field: SerializeField]
        public MultiStateButtonView InterestedButton { get; private set; }

        [field: SerializeField]
        public GameObject AttendeeContainer { get; private set; }

        [field: SerializeField]
        public RectTransform LayoutRoot { get; private set; }
    }
}
