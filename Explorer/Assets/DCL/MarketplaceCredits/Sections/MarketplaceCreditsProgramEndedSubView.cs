using DCL.Audio;
using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedSubView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Title { get; private set; }

        [field: SerializeField]
        public TMP_Text Subtitle { get; private set; }

        [field: SerializeField]
        public AudioClipConfig ClickOnLinksAudio { get; private set; }

        public void SetTitle(string title) =>
            Title.text = title;

        public void SetSubtitle(string subtitle) =>
            Subtitle.text = subtitle;

        public void PlayOnLinkClickAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickOnLinksAudio);
    }
}
