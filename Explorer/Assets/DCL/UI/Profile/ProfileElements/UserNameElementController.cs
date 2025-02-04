﻿using DCL.Profiles;
using DCL.UI.Profiles.Helpers;
using System;
using System.Threading;

namespace DCL.UI.ProfileElements
{
    public class UserNameElementController : IDisposable
    {
        public readonly UserNameElement Element;
        private readonly IProfileNameColorHelper profileNameColorHelper;

        private Profile currentProfile;

        public UserNameElementController(
            UserNameElement element,
            IProfileNameColorHelper profileNameColorHelper)
        {
            this.Element = element;
            this.profileNameColorHelper = profileNameColorHelper;

            element.CopyNameWarningNotification.Hide(true);

            element.CopyUserNameButton.onClick.AddListener(() =>
            {
                if (currentProfile == null)
                    return;

                UserInfoHelper.CopyToClipboard(currentProfile.HasClaimedName ? element.UserNameText.text : $"{currentProfile.Name}#{currentProfile.UserId[^4..]}");
                UserInfoHelper.ShowCopyWarningAsync(element.CopyNameWarningNotification, CancellationToken.None).Forget();
            });
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            Element.UserNameText.text = profile.Name;
            Element.UserNameText.color = profileNameColorHelper.GetNameColor(profile.Name);
            Element.UserNameHashtagText.text = $"#{profile.UserId[^4..]}";
            Element.UserNameHashtagText.gameObject.SetActive(!profile.HasClaimedName);
            Element.VerifiedMark.SetActive(profile.HasClaimedName);
        }


        public void Dispose()
        {
            Element.CopyUserNameButton.onClick.RemoveAllListeners();
            Element.CopyNameWarningNotification.Hide(true);
        }

    }
}
