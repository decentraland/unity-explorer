using DCL.Friends;
using DCL.Profiles;
using MVC;
using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListRowItemView : MonoBehaviour
    {
        [field: SerializeField] public MemberListSingleItemView LeftItem { get; private set; }
        [field: SerializeField] public MemberListSingleItemView RightItem { get; private set; }

        public void ConfigureLeft(Profile memberProfile, ViewDependencies viewDependencies)
        {
            LeftItem.gameObject.SetActive(true);
            LeftItem.InjectDependencies(viewDependencies);
            LeftItem.Configure(memberProfile);
        }

        public void ConfigureRight(Profile memberProfile, ViewDependencies viewDependencies)
        {
            RightItem.gameObject.SetActive(true);
            RightItem.InjectDependencies(viewDependencies);
            RightItem.Configure(memberProfile);
        }

        public void ResetElements()
        {
            LeftItem.gameObject.SetActive(false);
            RightItem.gameObject.SetActive(false);
        }

        public void SubscribeToInteractions(Action<Profile> mainButton,
            Action<Profile> contextMenuButton,
            Action<Profile, FriendshipStatus> friendButton)
        {
            LeftItem.RemoveAllListeners();
            RightItem.RemoveAllListeners();

            LeftItem.MainButtonClicked += mainButton;
            LeftItem.ContextMenuButtonClicked += contextMenuButton;
            LeftItem.FriendButtonClicked += friendButton;

            RightItem.MainButtonClicked += mainButton;
            RightItem.ContextMenuButtonClicked += contextMenuButton;
            RightItem.FriendButtonClicked += friendButton;
        }
    }
}
