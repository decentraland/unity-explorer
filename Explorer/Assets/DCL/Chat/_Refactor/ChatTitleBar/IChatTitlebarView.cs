using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DG.Tweening;
using System;

namespace DCL.Chat
{
    public interface IChatTitlebarView
    {
        void Initialize();
        event Action? CloseChatButtonClicked;
        event Action? CloseMemberListButtonClicked;
        event Action? HideMemberListButtonClicked;
        event Action? ShowMemberListButtonClicked;
        event Action OnCloseClicked;
        event Action<bool> OnMemberListToggled;
        void Show();
        void Hide();

        /// <summary>
        /// Updates both title-bar labels to display the current channel name.
        /// </summary>
        void SetChannelNameText(string channelName);
        
        /// <summary>
        /// Switches the title bar into “profile” mode; kicks off async load.
        /// </summary>
        /// <param name="userId">wallet/address of the target user</param>
        /// <param name="profileDataProvider">who gives us profile info</param>
        void SetupProfileView(Web3Address userId, ProfileRepositoryWrapper profileDataProvider);
        
        /// <summary>
        /// Switches between the “chat” vs “member list” variants of the title bar.
        /// </summary>
        /// <param name="isMemberListVisible">true → show member-list bar; false → show chat bar</param>
        void ChangeTitleBarVisibility(bool isMemberListVisible);

        /// <summary>
        /// Updates the little badge that shows how many people are in the list.
        /// </summary>
        void SetMemberListNumberText(string userAmount);
        
        /// <summary>
        /// Switches the title bar into “nearby channel” mode (icon + count, no profile).
        /// </summary>
        void SetNearbyChannelImage();

        void SetFocusedState(bool isFocused, bool animate, float duration, Ease easing);
    }
}