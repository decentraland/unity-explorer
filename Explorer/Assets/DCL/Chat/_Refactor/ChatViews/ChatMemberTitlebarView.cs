using DCL.Chat.ChatViewModels;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatViews
{
    public class ChatMemberTitlebarView : MonoBehaviour
    {
        public event Action OnCloseRequested;
        public event Action OnBackRequested;
        public Button ButtonClose => closeButton;
        public Button ButtonBack => backButton;

        [SerializeField] private Button closeButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text membersCountText;
        [SerializeField] private TMP_Text channelNameText;

        private void Awake()
        {
            closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
            backButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        }

        public void SetMemberCount(string count) =>
            membersCountText.text = count;

        public void Activate(bool activate) =>
            gameObject.SetActive(activate);

        public void SetChannelName(ChatTitlebarViewModel model)
        {
            if (channelNameText != null) { channelNameText.SetText(model.ViewMode == TitlebarViewMode.Nearby ? "Nearby  -" : $"{model.Username}  -"); }
        }
    }
}
