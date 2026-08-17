using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class PlaceElementView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public GameObject playerCounterContainer;

        [field: SerializeField]
        public ImageView placeImage;

        [field: SerializeField]
        public TMP_Text placeName;

        [field: SerializeField]
        public TMP_Text placeCreator;

        [field: SerializeField]
        public TMP_Text playersCount;

        [field: SerializeField]
        public Button resultButton;

        [field: SerializeField]
        public Image arrowImage;

        [field: SerializeField]
        public Animator resultAnimator;

        [field: SerializeField]
        public GameObject LiveContainer { get; private set; }

        private ImageController? imageController;
        private Sprite? placeholderImage;

        public Vector2Int coords;

        public bool IsHoverEnabled = true;

        public event Action<bool, Vector2Int> OnMouseHover;

        public void ConfigurePlaceImageController(ImageControllerProvider imageControllerProvider)
        {
            // The prefab authors the placeholder thumbnail on the image itself; capture it before the first
            // request overwrites it, so places that carry no thumbnail url still render something.
            placeholderImage = placeImage.ImageSprite;
            imageController = imageControllerProvider.Create(placeImage);
        }

        public void SetPlaceImage(string imageUrl) =>
            imageController?.RequestImage(imageUrl, true, defaultSprite: placeholderImage);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsHoverEnabled)
                OnMouseHover?.Invoke(true, coords);
            arrowImage.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (IsHoverEnabled)
                OnMouseHover?.Invoke(false, coords);
            arrowImage.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            resultAnimator.enabled = true;
        }

        private void OnDisable()
        {
            resultAnimator.enabled = false;
        }

        private void OnDestroy()
        {
            imageController?.Dispose();
        }
    }
}

