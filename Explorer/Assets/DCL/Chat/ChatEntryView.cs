using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        [field: SerializeField]
        internal RectTransform backgroundRectTransform { get; private set; }

        [field: SerializeField]
        internal RectTransform textRectTransform { get; private set; }

        [field: SerializeField]
        internal RectTransform rectTransform { get; private set; }

        [field: SerializeField]
        internal ContentSizeFitter contentSizeFitter { get; private set; }

        [field: SerializeField]
        internal TMP_Text playerName { get; private set; }

        [field: SerializeField]
        internal TMP_Text walletIdText { get; private set; }

        [field: SerializeField]
        internal Image playerIcon { get; private set; }

        [field: SerializeField]
        internal TMP_Text entryText { get; private set; }

        [field: SerializeField]
        internal Image verifiedIcon { get; private set; }

        [field: SerializeField]
        internal Image entryBackground { get; private set; }

        [field: SerializeField]
        internal CanvasGroup chatEntryCanvasGroup { get; private set; }

        private Vector2 backgroundSize;

        public void SetUsername(string username, string walletId)
        {
            playerName.text = username.Contains("#")
                ? $"{username.Substring(0, username.IndexOf("#", StringComparison.Ordinal))}"
                : username;
            walletIdText.text = $"#{walletId.Substring(0,5)}";

            walletIdText.gameObject.SetActive(username.Contains("#"));
            verifiedIcon.gameObject.SetActive(!username.Contains("#"));
        }

        public void AnimateChatEntry()
        {
            chatEntryCanvasGroup.alpha = 0;
            chatEntryCanvasGroup.DOFade(1, 0.5f);
        }

        public void SetItemData(ChatMessage data)
        {
            SetUsername(data.Sender, data.WalletAddress);
            entryText.text = data.Message;

            contentSizeFitter.SetLayoutVertical();
            backgroundSize = backgroundRectTransform.sizeDelta;
            backgroundSize.y = Mathf.Max(textRectTransform.sizeDelta.y + 40, 58);

            backgroundRectTransform.sizeDelta = backgroundSize;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, backgroundSize.y);
        }
    }
}
