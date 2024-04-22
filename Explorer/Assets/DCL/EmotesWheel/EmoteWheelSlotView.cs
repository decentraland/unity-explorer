using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.EmotesWheel
{
    public class EmoteWheelSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<int>? OnPlay;
        public event Action<int>? OnHover;

        [SerializeField]
        private Button playButton;

        [field: SerializeField]
        public Image BackgroundRarity { get; private set; }

        [SerializeField]
        private GameObject hoverBackground;

        [field: SerializeField]
        public Image Thumbnail { get; set; }

        [field: SerializeField]
        public GameObject EmptyContainer { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig EquipAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig UnEquipAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }

        public int Slot { get; set; }

        private void Awake()
        {
            playButton.onClick.AddListener(() => OnPlay?.Invoke(Slot));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverBackground.SetActive(true);
            OnHover?.Invoke(Slot);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverBackground.SetActive(false);
        }
    }
}
