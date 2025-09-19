using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.UI.Controls.Configs;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.UI
{
    /// <summary>
    /// Encapsulates the functionality related to user invitations to communities through a context menu submenu that is loaded depending on whether the local user
    /// can invite a remote user.
    /// To add this option to a context menu, call AddSubmenuControlToContextMenu when adding the controls to it. Before the context menu is shown, remember to set
    /// the remote user's wallet address with SetUserToInvite.
    /// </summary>
    public class CommunityInvitationContextMenuButtonHandler
    {
        private const string INVITATION_FAILED_TEXT = "Error sending invitation. Please try again.";
        private const string INVITE_SENT_TEXT = "Invite to Community Sent";
        private const string USER_POTENTIAL_INVITATIONS_FAILED_TEXT = "Error loading 'Invite to Community' menu option. Reopen menu to try again.";
        private const int MAXIMUM_HEIGHT_OF_SUBMENU = 600;
        private const int MAXIMUM_WIDTH_OF_SUBMENU = 300;
        private const float SUBMENU_ANCHOR_PADDING = 20;
        private static readonly RectOffset SUBMENU_VERTICAL_PADDINGS = new RectOffset(8, 8, 8, 12);

        private readonly RectOffset scrollViewPaddings = new ();
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly int subMenuItemSpacing;
        private string userWalletId;

        private readonly List<string> lastCommunityNames = new ();
        private GetInvitableCommunityListResponse.InvitableCommunityData[] lastCommunityData;
        private CancellationTokenSource invitationActionCts;

        /// <summary>
        /// Main constructor.
        /// </summary>
        /// <param name="communitiesDataProvider">The source of the data related to users and invitations.</param>
        /// <param name="subMenuItemSpacing">The distance among items in the submenu.</param>
        public CommunityInvitationContextMenuButtonHandler(CommunitiesDataProvider communitiesDataProvider, int subMenuItemSpacing)
        {
            this.communitiesDataProvider = communitiesDataProvider;
            this.subMenuItemSpacing = subMenuItemSpacing;
        }

        /// <summary>
        /// Adds a submenu button to a context menu whose final visibility will be resolved asynchronously (after the context menu is shown).
        /// If the user can't invite a remote user, the button will hide; if the user can invite, the submenu options will be then configured and will be available
        /// when the user hovers the submenu button.
        /// </summary>
        /// <param name="contextMenu">Any context menu to which to add the button.</param>
        /// <param name="offsetFromAnchor">The offset to apply to the submenu panel from the parent context menu's anchor.</param>
        /// <param name="buttonText">The text to show in the new button.</param>
        /// <param name="buttonIcon">The icon to show next to the new button.</param>
        public void AddSubmenuControlToContextMenu(GenericContextMenu contextMenu, Vector2 offsetFromAnchor, string buttonText, Sprite buttonIcon)
        {
            contextMenu.AddControl(new SubMenuContextMenuButtonSettings(buttonText,
                                                                        buttonIcon,
                                                                        new GenericContextMenu(MAXIMUM_WIDTH_OF_SUBMENU,
                                                                                         elementsSpacing: contextMenu.elementsSpacing,
                                                                                         offsetFromTarget: offsetFromAnchor,
                                                                                         verticalLayoutPadding: SUBMENU_VERTICAL_PADDINGS),
                                                                        anchorPadding: SUBMENU_ANCHOR_PADDING,
                                                                        asyncControlSettingsFillingDelegate: CreateInvitationSubmenuItemsAsync,
                                                                        asyncVisibilityResolverDelegate: ResolveInvitationSubmenuVisibilityAsync));
        }

        /// <summary>
        /// Replaces the remote user to which invitations will be sent when possible.
        /// </summary>
        /// <param name="walletId">The new wallet address.</param>
        public void SetUserToInvite(string walletId)
        {
            userWalletId = walletId;
        }

        private async UniTask<bool> ResolveInvitationSubmenuVisibilityAsync(CancellationToken ct)
        {
            // Asks the server for the data of the communities to which the user can be invited
            Result<GetInvitableCommunityListResponse> response = await communitiesDataProvider.GetInvitableCommunityListAsync(userWalletId, ct).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if(ct.IsCancellationRequested)
                return false;

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
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(USER_POTENTIAL_INVITATIONS_FAILED_TEXT));
            }

            return false;
        }

        private UniTask CreateInvitationSubmenuItemsAsync(GenericContextMenu contextSubMenu, CancellationToken ct)
        {
            if(ct.IsCancellationRequested)
                return UniTask.CompletedTask;

            // Adds the scroll view
            var scroll = new ScrollableButtonListControlSettings(subMenuItemSpacing,
                                                                 MAXIMUM_HEIGHT_OF_SUBMENU,
                                                                 OnInviteToCommunitySubmenuScrollViewItemClickedAsync,
                                                                 verticalLayoutPadding: scrollViewPaddings);
            contextSubMenu.AddControl(scroll);

            scroll.SetData(lastCommunityNames);

            return UniTask.CompletedTask;
        }

        private async void OnInviteToCommunitySubmenuScrollViewItemClickedAsync(int itemIndex)
        {
            invitationActionCts = invitationActionCts.SafeRestart();
            Result<string> result = await communitiesDataProvider.SendInviteOrRequestToJoinAsync(lastCommunityData[itemIndex].id, userWalletId, InviteRequestAction.invite, invitationActionCts.Token).SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (result.Success)
                NotificationsBusController.Instance.AddNotification(new DefaultSuccessNotification(INVITE_SENT_TEXT));
            else
                NotificationsBusController.Instance.AddNotification(new ServerErrorNotification(INVITATION_FAILED_TEXT));
        }
    }
}
