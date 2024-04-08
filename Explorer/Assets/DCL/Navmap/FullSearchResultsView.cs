using DCL.UI;
using DCL.WebRequests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class FullSearchResultsView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

        private ImageController imageController;

        public void ConfigurePlaceImageController(IWebRequestController webRequestController) =>
            imageController = new ImageController(placeImage, webRequestController);

        public void SetPlaceImage(string imageUrl) =>
            imageController.RequestImage(imageUrl, true);

        public void OnPointerEnter(PointerEventData eventData)
        {
            arrowImage.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            arrowImage.gameObject.SetActive(false);
        }
    }
}
