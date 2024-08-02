using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class DestinationSetView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Text { get; private set; }
        [field: SerializeField] public Button QuitButton { get; private set; }
        [field: SerializeField] public Image PinImage { get; private set; }
        [field: SerializeField] public GameObject PinIcon { get; private set; }
        [field: SerializeField] public GameObject PlaceIcon { get; private set; }

        public void Setup(string description, bool isMapPin, [CanBeNull] Sprite sprite)
        {
            PlaceIcon.SetActive(!isMapPin);
            PinIcon.SetActive(isMapPin);
            Text.text = description;
            if (isMapPin && sprite != null)
            {
                PinImage.sprite = sprite;
            }
        }
    }
}
