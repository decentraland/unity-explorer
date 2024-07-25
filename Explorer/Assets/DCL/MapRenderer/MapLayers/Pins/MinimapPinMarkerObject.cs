using UnityEngine;
using UnityEngine.UI;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public class MinimapPinMarkerObject : MonoBehaviour
    {
        [field: SerializeField] internal GameObject mapPinBackground { get; private set; }
        [field: SerializeField] internal GameObject scenePinBackground { get; private set; }
        [field: SerializeField] internal Image mapPinIcon { get; private set; }

        public void SetupAsScenePin()
        {
            mapPinBackground.SetActive(false);
            scenePinBackground.SetActive(true);
        }

        public void SetupAsMapPin(Sprite sprite)
        {
            mapPinBackground.SetActive(true);
            scenePinBackground.SetActive(false);
            mapPinIcon.sprite = sprite;
        }

        public void HidePin()
        {
            mapPinBackground.SetActive(false);
            scenePinBackground.SetActive(false);
        }

        public void SetLocation() { }
    }
}
