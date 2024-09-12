using UnityEngine;
using UnityEngine.UI;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public class MinimapPinMarkerObject : MonoBehaviour
    {
        [field: SerializeField] internal GameObject mapPinBackground { get; private set; }
        [field: SerializeField] internal GameObject scenePinBackground { get; private set; }
        [field: SerializeField] internal Image mapPinIcon { get; private set; }

        private bool isMapPin;

        public void SetupAsScenePin()
        {
            isMapPin = false;
            mapPinBackground.SetActive(false);
            scenePinBackground.SetActive(true);
        }

        public void SetupAsMapPin(Sprite sprite)
        {
            isMapPin = true;
            mapPinBackground.SetActive(true);
            scenePinBackground.SetActive(false);
            mapPinIcon.sprite = sprite;
        }

        public void HidePin()
        {
            mapPinBackground.SetActive(false);
            scenePinBackground.SetActive(false);
        }

        public void RestorePin()
        {
            mapPinBackground.SetActive(isMapPin);
            scenePinBackground.SetActive(!isMapPin);
        }

        public void SetPosition(Vector2 position)
        {
            transform.localPosition = position;
        }
    }
}
