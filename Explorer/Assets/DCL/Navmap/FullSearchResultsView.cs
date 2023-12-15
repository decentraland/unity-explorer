using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class FullSearchResultsView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField]
        public Image placeImage;

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
