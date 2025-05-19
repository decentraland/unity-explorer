using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatEntryUsernameElement : MonoBehaviour
    {
        public Action UserNameClicked;

        [field: SerializeField] internal TMP_Text userName { get; private set; }
        [field: SerializeField] internal TMP_Text walletIdText { get; private set; }
        [field: SerializeField] internal Image? verifiedIcon { get; private set; }
        [field: SerializeField] internal ChatEntryUsernameClickDetectionHandler usernameClickDetection { get; private set; }

        private void Awake()
        {
            if (usernameClickDetection != null)
                usernameClickDetection.UserNameClicked += UserNameClickDetected;
        }

        private void UserNameClickDetected()
        {
            UserNameClicked?.Invoke();
        }

        public void SetUsername(string username, string? walletId)
        {
            userName.text = username;
            walletIdText.text = walletId;

            bool hasWalletId = !string.IsNullOrEmpty(walletId);

            walletIdText.gameObject.SetActive(hasWalletId);
            verifiedIcon?.gameObject.SetActive(!hasWalletId);
        }

        public float GetUserNamePreferredWidth(float backgroundWidthOffset, float verifiedBadgeWidth) =>
            userName.preferredWidth + Math.Max(walletIdText.preferredWidth, verifiedBadgeWidth) + backgroundWidthOffset;

        public void GetRightEdgePosition(Vector3[] corners)
        {
            if (verifiedIcon != null && verifiedIcon.gameObject.activeSelf)
            {
                RectTransform iconTransform = verifiedIcon.rectTransform;
                iconTransform.GetWorldCorners(corners);
            }
            else if (walletIdText.gameObject.activeSelf)
            {
                RectTransform walletTransform = walletIdText.rectTransform;
                walletTransform.GetWorldCorners(corners);
            }
            else
            {
                RectTransform textTransform = userName.rectTransform;
                textTransform.GetWorldCorners(corners);
            }
        }
    }
}
