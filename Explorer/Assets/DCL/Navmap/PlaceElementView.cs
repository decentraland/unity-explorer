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

        private ImageController imageController;

        public Vector2Int coords;

        public event Action<bool, Vector2Int> OnMouseHover;

        public void ConfigurePlaceImageController(ISpriteCache spriteCache) =>
            imageController = new ImageController(placeImage, spriteCache);

        public void SetPlaceImage(string imageUrl) =>
            imageController.RequestImage(imageUrl, true);

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnMouseHover?.Invoke(true, coords);
            arrowImage.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
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
    }
}

