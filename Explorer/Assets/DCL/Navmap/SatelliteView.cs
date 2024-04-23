using DCL.Audio;
using DCL.MapRenderer.ConsumerUtils;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Navmap
{
    public class SatelliteView : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnClickedGenesisCityLink;

        [field: SerializeField]
        public TMP_Text SatelliteCreditsText;

        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(SatelliteCreditsText, eventData.position, null);

            if (linkIndex != -1)
                OnClickedGenesisCityLink?.Invoke();
        }
    }
}
