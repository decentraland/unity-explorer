using DCL.Chat;
using DCL.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserBasicInfo_PassportModuleController : IPassportModuleController
    {
        private UserBasicInfo_PassportModuleView view;
        private Profile currentProfile;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        public UserBasicInfo_PassportModuleController(UserBasicInfo_PassportModuleView view, ChatEntryConfigurationSO chatEntryConfiguration)
        {
            this.view = view;
            this.chatEntryConfiguration = chatEntryConfiguration;

            view.CopyUserNameButton.onClick.AddListener(() => CopyToClipboard(view.UserNameText.text));
            view.CopyWalletAddressButton.onClick.AddListener(() => CopyToClipboard(currentProfile?.UserId));
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            view.UserNameText.text = profile.Name;
            view.UserNameText.color = chatEntryConfiguration.GetNameColor(profile.Name);
            view.VerifiedMark.SetActive(profile.HasClaimedName);
            view.UserWalletAddressText.text = $"{profile.UserId[..3]}...{profile.UserId[^3..]}";

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.UserNameContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.WalletAddressContainer);
        }

        public void Dispose()
        {
            view.CopyUserNameButton.onClick.RemoveAllListeners();
            view.CopyWalletAddressButton.onClick.RemoveAllListeners();
        }

        private void CopyToClipboard(string text) =>
            GUIUtility.systemCopyBuffer = text;
    }
}
