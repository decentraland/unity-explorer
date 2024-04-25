using DCL.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiSectionToggle : MonoBehaviour
    {
        [field: SerializeField]
        public Toggle SectionToggle { get; private set; }

        [field: SerializeField]
        public Image SectionImage { get; private set; }

        [field: SerializeField]
        public Color SelectedColor { get; private set; }

        [field: SerializeField]
        public Color UnselectedColor { get; private set; }

        [field: SerializeField]
        public EmojiSectionName SectionName { get; private set; }

        [field: SerializeField]
        public float SectionPosition { get; private set; }
        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ToggleAudio { get; private set; }

        private void Start() =>
            SectionToggle.onValueChanged.AddListener(OnValueChanged);

        private void OnValueChanged(bool isOn)
        {
            if (isOn)
                UIAudioEventsBus.Instance.SendPlayAudioEvent(ToggleAudio);

            SectionImage.color = isOn ? SelectedColor : UnselectedColor;
        }
    }
}
