using DCL.Audio;
using DCL.MapRenderer.ConsumerUtils;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.Navmap
{
    public class SatelliteView : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler
    {
        public event Action OnClickedGenesisCityLink;

        [field: SerializeField]
        public TMP_Text SatelliteCreditsText;

        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickAudio);

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(SatelliteCreditsText, eventData.position, null);

            if (linkIndex != -1)
                OnClickedGenesisCityLink?.Invoke();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
        }
    }
}
