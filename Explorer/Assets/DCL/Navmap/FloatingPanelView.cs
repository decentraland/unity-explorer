using DCL.Audio;
using DCL.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class FloatingPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject[] categories;

        [field: SerializeField]
        public string[] categoryNames;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; }

        [field: SerializeField]
        public RectTransform CategoriesContainer { get; private set; }

        [field: SerializeField]
        public GameObject PlaceSection { get; private set; }

        [field: SerializeField]
        public GameObject MapPinSection { get; private set; }

        [field: SerializeField]
        public TMP_Text MapPinTitle { get; private set; }

        [field: SerializeField]
        public TMP_Text MapPinDescription { get; private set; }

        [field: SerializeField]
        public ImageView placeImage;

        [field: SerializeField]
        public Button closeButton;

        [field: SerializeField]
        public Button backButton;

        [field: SerializeField]
        public Button jumpInButton;

        [field: SerializeField]
        public TMP_Text visits;

        [field: SerializeField]
        public TMP_Text upvotes;

        [field: SerializeField]
        public TMP_Text location;

        [field: SerializeField]
        public TMP_Text placeName;

        [field: SerializeField]
        public TMP_Text placeCreator;

        [field: SerializeField]
        public TMP_Text placeDescription;

        [field: SerializeField]
        public TMP_Text parcelsCount;

        [field: SerializeField]
        public GameObject appearsIn;

        [field: SerializeField]
        public MultiStateButtonView likeButton;

        [field: SerializeField]
        public MultiStateButtonView dislikeButton;

        [field: SerializeField]
        public MultiStateButtonView favoriteButton;

        [field: SerializeField]
        public Animator panelAnimator;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig OnShowAudio { get; private set; }
    }
}
