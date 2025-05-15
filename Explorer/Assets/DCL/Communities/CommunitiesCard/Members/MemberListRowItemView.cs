using DCL.Friends;
using DCL.Profiles;
using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListRowItemView : MonoBehaviour
    {
        [field: SerializeField] public MemberListSingleItemView LeftItem { get; private set; }
        [field: SerializeField] public MemberListSingleItemView RightItem { get; private set; }

        private event Action<Profile>? MainButtonClicked;
        private event Action<Profile>? ContextMenuButtonClicked;
        private event Action<Profile, FriendshipStatus>? FriendButtonClicked;

        public void ConfigureLeft(Profile memberProfile) =>
            LeftItem.Configure(memberProfile);

        public void ConfigureRight(Profile memberProfile) =>
            RightItem.Configure(memberProfile);

        private void Awake()
        {
            LeftItem.MainButtonClicked += MainButtonClicked;
            LeftItem.ContextMenuButtonClicked += ContextMenuButtonClicked;
            LeftItem.FriendButtonClicked += FriendButtonClicked;

            RightItem.MainButtonClicked += MainButtonClicked;
            RightItem.ContextMenuButtonClicked += ContextMenuButtonClicked;
            RightItem.FriendButtonClicked += FriendButtonClicked;
        }

        public void SubscribeToInteractions(Action<Profile> mainButton,
            Action<Profile> contextMenuButton,
            Action<Profile, FriendshipStatus> friendButton)
        {
            MainButtonClicked = null;
            ContextMenuButtonClicked = null;
            FriendButtonClicked = null;

            MainButtonClicked += mainButton;
            ContextMenuButtonClicked += contextMenuButton;
            FriendButtonClicked += friendButton;
        }
    }
}
