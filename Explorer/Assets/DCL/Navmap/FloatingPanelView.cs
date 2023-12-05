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
        public RectTransform contentViewport;

        [field: SerializeField]
        public RectTransform descriptionContent;

        [field: SerializeField]
        public RectTransform rectTransform;

        [field: SerializeField]
        public RectTransform CategoriesContainer { get; private set; }

        [field: SerializeField]
        public Image placeImage;

        [field: SerializeField]
        public Button closeButton;

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
        public MultiStateButtonView likeButton;

        [field: SerializeField]
        public MultiStateButtonView dislikeButton;

        [field: SerializeField]
        public MultiStateButtonView favoriteButton;
    }
}
