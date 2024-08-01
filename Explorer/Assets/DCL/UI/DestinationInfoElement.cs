using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class DestinationInfoElement : MonoBehaviour
    {
        [field: SerializeField] public Button QuitButton { get; private set; }
        [field: SerializeField] private Image pinImage { get; set; }
        [field: SerializeField] private GameObject pinIcon { get; set; }
        [field: SerializeField] private GameObject placeIcon { get; set; }
        [field: SerializeField] private TMP_Text title { get; set; }

        public void Setup(string title, bool isMapPin, [CanBeNull] Sprite sprite)
        {
            placeIcon.SetActive(!isMapPin);
            pinIcon.SetActive(isMapPin);
            this.title.text = title;

            if (isMapPin && sprite != null) { pinImage.sprite = sprite; }
        }
    }
}
