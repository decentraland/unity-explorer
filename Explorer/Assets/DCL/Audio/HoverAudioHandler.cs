using DCL.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI
{
    public class HoverAudioHandler: MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            UIAudioEventsBus.Instance.SendPlayAudioEvent(HoverAudio);
        }

        public void OnPointerExit(PointerEventData eventData) { }
    }
}
