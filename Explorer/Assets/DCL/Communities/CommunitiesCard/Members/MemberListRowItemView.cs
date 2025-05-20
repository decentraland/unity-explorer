using MVC;
using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MemberListRowItemView : MonoBehaviour
    {
        [field: SerializeField] public MemberListSingleItemView LeftItem { get; private set; }
        [field: SerializeField] public MemberListSingleItemView RightItem { get; private set; }

        public void ConfigureLeft(GetCommunityMembersResponse.MemberData memberProfile, ViewDependencies viewDependencies)
        {
            LeftItem.gameObject.SetActive(true);
            LeftItem.InjectDependencies(viewDependencies);
            LeftItem.Configure(memberProfile);
        }

        public void ConfigureRight(GetCommunityMembersResponse.MemberData memberProfile, ViewDependencies viewDependencies)
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

        public void SubscribeToInteractions(Action<GetCommunityMembersResponse.MemberData> mainButton,
            Action<GetCommunityMembersResponse.MemberData, Vector2, MemberListSingleItemView> contextMenuButton,
            Action<GetCommunityMembersResponse.MemberData, FriendshipStatus> friendButton)
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
