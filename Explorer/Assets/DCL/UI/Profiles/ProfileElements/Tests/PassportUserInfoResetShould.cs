using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ProfileElements.Tests
{
    public class PassportUserInfoResetShould
    {
        private static void Wire(object target, string propertyName, object value) =>
            target.GetType()
                  .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                  .SetValue(target, value);

        private static TMP_Text NewText() =>
            new GameObject("text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();

        private static Button NewButton() =>
            new GameObject("button", typeof(Button)).GetComponent<Button>();

        private static WarningNotificationView NewWarning() =>
            new GameObject("warning", typeof(WarningNotificationView)).GetComponent<WarningNotificationView>();

        [Test]
        public void ResetTheNameSectionOnReuse()
        {
            var element = new GameObject("userName").AddComponent<UserNameElement>();
            TMP_Text nameText = NewText();
            TMP_Text hashtagText = NewText();
            var verifiedMark = new GameObject("verified");
            var officialMark = new GameObject("official");
            Wire(element, nameof(UserNameElement.UserNameText), nameText);
            Wire(element, nameof(UserNameElement.UserNameHashtagText), hashtagText);
            Wire(element, nameof(UserNameElement.VerifiedMark), verifiedMark);
            Wire(element, nameof(UserNameElement.OfficialMark), officialMark);
            Wire(element, nameof(UserNameElement.CopyUserNameButton), NewButton());
            Wire(element, nameof(UserNameElement.CopyNameWarningNotification), NewWarning());

            var presenter = new UserNameElementPresenter(element);

            //Arrange: a profile's identity is rendered into the shared element
            nameText.text = "SomeUser";
            hashtagText.text = "#1a2b";
            hashtagText.gameObject.SetActive(true);
            verifiedMark.SetActive(true);
            officialMark.SetActive(true);

            //Act
            presenter.Clear();

            //Assert
            Assert.AreEqual(string.Empty, nameText.text);
            Assert.AreEqual(string.Empty, hashtagText.text);
            Assert.IsFalse(hashtagText.gameObject.activeSelf);
            Assert.IsFalse(verifiedMark.activeSelf);
            Assert.IsFalse(officialMark.activeSelf);
        }

        [Test]
        public void ResetTheWalletSectionOnReuse()
        {
            var element = new GameObject("wallet").AddComponent<UserWalletAddressElement>();
            TMP_Text walletText = NewText();
            Wire(element, nameof(UserWalletAddressElement.UserWalletAddressText), walletText);
            Wire(element, nameof(UserWalletAddressElement.CopyWalletAddressButton), NewButton());
            Wire(element, nameof(UserWalletAddressElement.CopyWalletWarningNotification), NewWarning());

            var presenter = new UserWalletAddressElementPresenter(element);

            presenter.Setup("0x1234567890abcdef1234567890abcdef12345678");
            Assert.IsNotEmpty(walletText.text);

            presenter.Clear();

            Assert.AreEqual(string.Empty, walletText.text);
        }
    }
}
