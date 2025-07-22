using DCL.Audio;
using TMPro;
using UnityEngine;

namespace DCL.MarketplaceCredits.Sections
{
    public class MarketplaceCreditsProgramEndedSubView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text TitleBolded { get; private set; }
        
        [field: SerializeField]
        public TMP_Text TitleNormal { get; private set; }

        [field: SerializeField]
        public TMP_Text Subtitle { get; private set; }

        [field: SerializeField]
        public AudioClipConfig ClickOnLinksAudio { get; private set; }

        public void SetBoldTitle(string title) =>
            TitleBolded.text = title;

        public void SetNormalTitle(string title)
        {
            TitleNormal.text = title;
            TitleNormal.gameObject.SetActive(!string.IsNullOrEmpty(title)); 
        }

        public void SetSubtitle(string subtitle) =>
            Subtitle.text = subtitle;

        public void PlayOnLinkClickAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(ClickOnLinksAudio);
    }
}
