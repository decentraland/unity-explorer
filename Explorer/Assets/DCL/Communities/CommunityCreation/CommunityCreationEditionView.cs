using DCL.Audio;
using DCL.UI;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionView : ViewBase, IView
    {
        public Action GetNameButtonClicked;

        [SerializeField] public Button backgroundCloseButton;
        [SerializeField] public Button cancelButton;
        [SerializeField] private GameObject getNamePanel;
        [SerializeField] private TMP_Text getNamePanelDescriptionText;
        [SerializeField] private Button getNameButton;
        [SerializeField] private GameObject creationPanel;
        [SerializeField] private AudioClipConfig clickOnLinksAudio;

        private void Awake()
        {
            getNameButton.onClick.AddListener(() => GetNameButtonClicked?.Invoke());
        }

        private void OnDestroy()
        {
            getNameButton.onClick.RemoveAllListeners();
        }

        public void SetAsClaimedName(bool hasClaimedName)
        {
            getNamePanel.SetActive(!hasClaimedName);
            creationPanel.SetActive(hasClaimedName);
        }

        public void ConvertGetNameDescriptionUrlsToClickableLinks(Action<string> onLinkClicked) =>
            getNamePanelDescriptionText.ConvertUrlsToClickeableLinks(onLinkClicked);

        public void PlayOnLinkClickAudio() =>
            UIAudioEventsBus.Instance.SendPlayAudioEvent(clickOnLinksAudio);
    }
}
