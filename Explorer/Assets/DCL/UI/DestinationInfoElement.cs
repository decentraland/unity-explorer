using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class DestinationInfoElement : MonoBehaviour
    {
        [field: SerializeField] public Button QuitButton { get; private set; }
        [field: SerializeField] private Image PinImage { get; set; }
        [field: SerializeField] private GameObject PinIcon { get; set; }
        [field: SerializeField] private GameObject PlaceIcon { get; set; }
        [field: SerializeField] private TMP_Text Title { get; set; }

        public void Setup(string title, bool isMapPin, Sprite? sprite)
        {
            PlaceIcon.SetActive(!isMapPin);
            PinIcon.SetActive(isMapPin);
            Title.text = title;

            if (isMapPin && sprite != null) { PinImage.sprite = sprite; }
        }
    }
}
