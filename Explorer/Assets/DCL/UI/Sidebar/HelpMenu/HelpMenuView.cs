using MVC;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar.HelpMenu
{
    public class HelpMenuView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] private Button mouseAndKeyControlsButton { get; set; } = null!;
        [field: SerializeField] private Button faqButton { get; set; } = null!;
        [field: SerializeField] private Button contactSupportButton { get; set; } = null!;
        [field: SerializeField] private Button discordButton { get; set; } = null!;

        public event Action? MouseAndKeyControlsClicked;
        public event Action? FaqClicked;
        public event Action? ContactSupportClicked;
        public event Action? DiscordClicked;

        private void Awake()
        {
            mouseAndKeyControlsButton.onClick.AddListener(() => MouseAndKeyControlsClicked?.Invoke());
            faqButton.onClick.AddListener(() => FaqClicked?.Invoke());
            contactSupportButton.onClick.AddListener(() => ContactSupportClicked?.Invoke());
            discordButton.onClick.AddListener(() => DiscordClicked?.Invoke());
        }
    }
}
