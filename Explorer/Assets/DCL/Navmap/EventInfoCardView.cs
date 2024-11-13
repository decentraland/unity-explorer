using DCL.Audio;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class EventInfoCardView : MonoBehaviour
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
        public ImageView MapPinPlaceImage { get; private set; }

        [field: SerializeField]
        public ImageView placeImage;

        [field: SerializeField]
        public Button closeButton;

        [field: SerializeField]
        public Button mapPinCloseButton { get; private set; }

        [field: SerializeField]
        public Button backButton;

        [field: SerializeField]
        public Button jumpInButton;

        [field: SerializeField]
        public Button setAsDestinationButton { get; private set; }

        [field: SerializeField]
        public Button setAsDestinationMapPinButton { get; private set; }
        [field: SerializeField]
        public Button removeDestinationButton { get; private set; }
        [field: SerializeField]
        public Button removeMapPinDestinationButton { get; private set; }

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

        internal event Action? onPointerEnterAction;
        internal event Action? onPointerExitAction;

        public void OnPointerEnter() =>
            onPointerEnterAction?.Invoke();

        public void OnPointerExit() =>
            onPointerExitAction?.Invoke();
    }
}
