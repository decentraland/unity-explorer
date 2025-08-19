using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities.Extensions;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.UI.GenericContextMenu.Controllers
{
    public class CommunityInvitationContextMenuButtonHandler
    {
        private readonly CommunitiesDataProvider communitiesDataProvider;
        private readonly int subMenuitemSpacing;
        private string userWalletId;

        private readonly List<string> lastCommunityNames = new List<string>();
        private GetUserCommunitiesData.CommunityData[] lastCommunityData;
        private CancellationTokenSource invitationActionCts;

        public CommunityInvitationContextMenuButtonHandler(CommunitiesDataProvider communitiesDataProvider, int subMenuitemSpacing)
        {
            this.communitiesDataProvider = communitiesDataProvider;
            this.subMenuitemSpacing = subMenuitemSpacing;
        }

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

        public void SetUserToInvite(string walletId)
        {
            userWalletId = walletId;
        }

        private async UniTask<bool> ResolveInvitationSubmenuVisibilityAsync(CancellationToken ct)
        {
            if(ct.IsCancellationRequested)
                return false;

            // Asks the server for the data of the communities to which the user can be invited
            Result<GetUserCommunitiesResponse> response = await communitiesDataProvider.GetUserCommunitiesAsync(string.Empty, true, 0, 99, ct).SuppressToResultAsync();

            if (response.Success)
            {
                lastCommunityData = response.Value.data.results;
                lastCommunityNames.Clear();

                foreach (GetUserCommunitiesData.CommunityData community in lastCommunityData)
                    if (community.role == CommunityMemberRole.moderator || community.role == CommunityMemberRole.owner) // TODO: remove this once the endpoint is implemented
                        lastCommunityNames.Add(community.name);

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
            var scroll = new ScrollableButtonListControlSettings(subMenuitemSpacing, 600, OnInviteToCommunitySubmenuScrollViewItemClicked);
            contextSubMenu.AddControl(scroll);

            scroll.SetData(lastCommunityNames);

            return UniTask.CompletedTask;
        }

        private async void OnInviteToCommunitySubmenuScrollViewItemClicked(int itemIndex)
        {
            Debug.Log(itemIndex);

            invitationActionCts = invitationActionCts.SafeRestart();
            Result<bool> result = await communitiesDataProvider.SendInviteOrRequestToJoinAsync(lastCommunityData[itemIndex].id, userWalletId, InviteRequestAction.invite, invitationActionCts.Token).SuppressToResultAsync();

            if (result.Success && result.Value)
            {
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
