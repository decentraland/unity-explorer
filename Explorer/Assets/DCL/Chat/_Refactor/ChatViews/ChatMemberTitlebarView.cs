using DCL.Chat.ChatViewModels;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatViews
{
    public class ChatMemberTitlebarView : MonoBehaviour
    {
        public event Action? OnCloseRequested;
        public event Action? OnBackRequested;
        public Button ButtonClose => closeButton;
        public Button ButtonBack => backButton;

        [SerializeField] private Button closeButton = null!;
        [SerializeField] private Button backButton = null!;
        [SerializeField] private TMP_Text membersCountText = null!;
        [SerializeField] private TMP_Text channelNameText = null!;

        private void Awake()
        {
            closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
            backButton.onClick.AddListener(() => OnBackRequested?.Invoke());
        }

        public void SetMemberCount(string count) =>
            membersCountText.text = count;

        public void SetMemberCountVisible(bool visible) =>
            membersCountText.gameObject.SetActive(visible);

        public void Activate(bool activate) =>
            gameObject.SetActive(activate);

        public void SetChannelName(ChatTitlebarViewModel model)
        {
            if (channelNameText != null)
                channelNameText.SetText(model.ViewMode == TitlebarViewMode.Nearby ? "Nearby  -" : $"{model.Username}  -");
        }
    }
}
