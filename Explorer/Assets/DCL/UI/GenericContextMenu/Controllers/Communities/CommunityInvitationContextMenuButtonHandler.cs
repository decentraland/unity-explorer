using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.NotificationsBusController.NotificationTypes;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.UI.GenericContextMenu.Controllers.Communities
{
    /// <summary>
    ///
    /// </summary>
    public class CommunityInvitationContextMenuButtonHandler
    {
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly INotificationsBusController notificationsBus;
        private readonly int subMenuItemSpacing;
        private string userWalletId;

        private readonly List<string> lastCommunityNames = new List<string>();
        private GetInvitableCommunityListResponse.InvitableCommunityData[] lastCommunityData;
        private CancellationTokenSource invitationActionCts;

        public CommunityInvitationContextMenuButtonHandler(CommunitiesDataProvider communitiesDataProvider, INotificationsBusController notificationsBus, int subMenuItemSpacing)
        {
            this.communitiesDataProvider = communitiesDataProvider;
            this.subMenuItemSpacing = subMenuItemSpacing;
            this.notificationsBus = notificationsBus;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="contextMenu"></param>
        /// <param name="buttonText"></param>
        /// <param name="buttonIcon"></param>
        public void AddSubmenuControlToContextMenu(GenericContextMenuParameter.GenericContextMenu contextMenu, string buttonText, Sprite buttonIcon)
        {
            contextMenu.AddControl(new SubMenuContextMenuButtonSettings(buttonText,
                                                                        buttonIcon,
                                                                        new GenericContextMenuParameter.GenericContextMenu(contextMenu.width,
                                                                                         verticalLayoutPadding: contextMenu.verticalLayoutPadding,
                                                                                         elementsSpacing: contextMenu.elementsSpacing,
                                                                                         offsetFromTarget: contextMenu.offsetFromTarget),
                                                                        asyncControlSettingsFillingDelegate: CreateInvitationSubmenuItemsAsync,
                                                                        asyncVisibilityResolverDelegate: ResolveInvitationSubmenuVisibilityAsync));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="walletId"></param>
        public void SetUserToInvite(string walletId)
        {
            userWalletId = walletId;
        }

        private async UniTask<bool> ResolveInvitationSubmenuVisibilityAsync(CancellationToken ct)
        {
            if(ct.IsCancellationRequested)
                return false;

            // Asks the server for the data of the communities to which the user can be invited
            Result<GetInvitableCommunityListResponse> response = await communitiesDataProvider.GetInvitableCommunityList(userWalletId, ct).SuppressToResultAsync();

            if (response.Success)
            {
                lastCommunityData = response.Value.data;
                lastCommunityNames.Clear();

                foreach (GetInvitableCommunityListResponse.InvitableCommunityData communityData in lastCommunityData)
                    lastCommunityNames.Add(communityData.name);

                if(lastCommunityNames.Count > 0)
                    return true;
            }
            else
            {
                // TODO
            }

            return false;
        }

        private UniTask CreateInvitationSubmenuItemsAsync(GenericContextMenuParameter.GenericContextMenu contextSubMenu, CancellationToken ct)
        {
            if(ct.IsCancellationRequested)
                return UniTask.CompletedTask;

            // Adds the scroll view
            var scroll = new ScrollableButtonListControlSettings(subMenuItemSpacing, 600, OnInviteToCommunitySubmenuScrollViewItemClicked);
            contextSubMenu.AddControl(scroll);

            scroll.SetData(lastCommunityNames);

            return UniTask.CompletedTask;
        }

        private async void OnInviteToCommunitySubmenuScrollViewItemClicked(int itemIndex)
        {
            invitationActionCts = invitationActionCts.SafeRestart();
            Result<bool> result = await communitiesDataProvider.SendInviteOrRequestToJoinAsync(lastCommunityData[itemIndex].id, userWalletId, InviteRequestAction.invite, invitationActionCts.Token).SuppressToResultAsync();

            if (result.Success && result.Value)
            {
                notificationsBus.AddNotification(new InvitationToCommunitySentNotification() { Type = NotificationType.INVITATION_TO_COMMUNITY_SENT });
                Debug.Log($"INVITED! ({userWalletId}) to {lastCommunityData[itemIndex].name} ({lastCommunityData[itemIndex].id})");
            }
            else
            {
                Debug.LogError($"NOT INVITED! ({userWalletId}) to {lastCommunityData[itemIndex].name} ({lastCommunityData[itemIndex].id})");
                // TODO
            }
        }
    }
}
