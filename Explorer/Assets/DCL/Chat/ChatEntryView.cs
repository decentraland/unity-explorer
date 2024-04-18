using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryView : MonoBehaviour
    {
        private const float BACKGROUND_HEIGHT_OFFSET = 56;
        private const float BACKGROUND_WIDTH_OFFSET = 56;
        private const float MAX_ENTRY_WIDTH = 246;

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
        internal Image ProfileBackground { get; private set; }

        [field: SerializeField]
        internal Image ProfileOutline { get; private set; }

        [field: SerializeField]
        internal CanvasGroup chatEntryCanvasGroup { get; private set; }

        private Vector2 backgroundSize;

        public void SetUsername(string username, string walletId)
        {
            if (string.IsNullOrEmpty(walletId))
            {
                playerName.text = username;
                walletIdText.gameObject.SetActive(false);
                verifiedIcon.gameObject.SetActive(false);
                return;
            }

            int walletIdIndexOf = username.IndexOf("#", StringComparison.Ordinal);

            playerName.text = username.Contains("#")
                ? $"{username.Substring(0, walletIdIndexOf)}"
                : username;

            walletIdText.text = walletIdIndexOf == -1 ? string.Empty : $"#{walletId.Substring(walletId.Length - 4)}";
            walletIdText.gameObject.SetActive(walletIdIndexOf != -1);
            verifiedIcon.gameObject.SetActive(walletIdIndexOf == -1);
        }

        public void AnimateChatEntry()
        {
            chatEntryCanvasGroup.alpha = 0;
            chatEntryCanvasGroup.DOFade(1, 0.5f);
        }

        public void SetItemData(ChatMessage data)
        {
            SetUsername(data.Sender, data.WalletAddress);
            entryText.SetText(data.Message);

            //Force mesh is needed otherwise entryText.GetParsedText() in CalculatePreferredWidth will return the original text
            //of the previous frame
            entryText.ForceMeshUpdate();

            contentSizeFitter.SetLayoutVertical();
            backgroundSize = backgroundRectTransform.sizeDelta;
            backgroundSize.y = Mathf.Max(textRectTransform.sizeDelta.y + BACKGROUND_HEIGHT_OFFSET);
            backgroundSize.x = CalculatePreferredWidth(data.Message);
            backgroundRectTransform.sizeDelta = backgroundSize;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, backgroundSize.y);
        }

        private float CalculatePreferredWidth(string messageContent)
        {
            if (playerName.text.Length + walletIdText.text.Length > (GetEmojisCount(messageContent) > 0 ? entryText.GetParsedText().Length + GetEmojisCount(messageContent) : entryText.GetParsedText().Length))
                return playerName.preferredWidth + walletIdText.preferredWidth + BACKGROUND_WIDTH_OFFSET;

            if(entryText.GetPreferredValues(messageContent, MAX_ENTRY_WIDTH, 0).x < MAX_ENTRY_WIDTH - BACKGROUND_WIDTH_OFFSET)
                return entryText.GetPreferredValues(messageContent, MAX_ENTRY_WIDTH, 0).x + BACKGROUND_WIDTH_OFFSET;

            return MAX_ENTRY_WIDTH;
        }

        private int GetEmojisCount(string message) =>
            message.Split("\\U0").Length - 1;
    }
}
