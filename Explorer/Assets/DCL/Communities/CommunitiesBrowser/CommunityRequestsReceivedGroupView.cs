using DCL.Communities.CommunitiesCard;
using DCL.Communities.CommunitiesCard.Members;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.UI;
using DCL.UI.Controls.Configs;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunityRequestsReceivedGroupView : MonoBehaviour
    {
        private const int REQUESTS_RECEIVED_MEMBER_CARDS_POOL_DEFAULT_CAPACITY = 5;
        private static readonly Vector2 ITEM_CONTEXT_MENU_SUBMENU_OFFSET = new (0.0f, -26.0f);

        public event Action<string>? CommunityButtonClicked;
        public event Action<ICommunityMemberData, InviteRequestIntention>? ElementManageRequestClicked;
        public event Action<UserProfileContextMenuControlSettings.UserData, UserProfileContextMenuControlSettings.FriendshipStatus>? ContextMenuUserProfileButtonClicked;
        public event Action<ICommunityMemberData>? OpenProfilePassportRequested;
        public event Action<ICommunityMemberData>? OpenUserChatRequested;
        public event Action<ICommunityMemberData>? CallUserRequested;
        public event Action<ICommunityMemberData>? BlockUserRequested;

        [SerializeField] public ImageView communityThumbnail = null!;
        [SerializeField] private TMP_Text communityTitle = null!;
        [SerializeField] private TMP_Text requestsReceivedText = null!;
        [SerializeField] private Button communityButton = null!;
        [SerializeField] private MemberListItemView requestReceivedMemberPrefab = null!;
        [SerializeField] private Transform requestReceivedMembersGridContainer = null!;

        [Header("Assets")]
        [SerializeField] private CommunityMemberListContextMenuConfiguration contextMenuSettings = null!;

        private string? currentCommunityId;
        private IObjectPool<MemberListItemView>? requestsReceivedMembersPool;
        private readonly List<MemberListItemView> currentRequestReceivedMembers = new ();
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private GenericContextMenu? contextMenu;
        private UserProfileContextMenuControlSettings? userProfileContextMenuControlSettings;
        private ICommunityMemberData lastClickedProfileCtx = null!;
        private GenericContextMenuElement? blockUserContextMenuElement;
        private CancellationTokenSource contextMenuCts = new ();
        private CommunityInvitationContextMenuButtonHandler? invitationButtonHandler;
        private CommunitiesDataProvider.CommunitiesDataProvider? communitiesDataProvider;

        private void Awake()
        {
            communityButton.onClick.AddListener(() =>
            {
                if (currentCommunityId != null)
                    CommunityButtonClicked?.Invoke(currentCommunityId);
            });

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding, elementsSpacing: contextMenuSettings.ElementsSpacing, showRim: true)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings((user, friendshipStatus) => ContextMenuUserProfileButtonClicked?.Invoke(user, friendshipStatus), showProfilePicture: false))
                         .AddControl(new SeparatorContextMenuControlSettings(contextMenuSettings.SeparatorHeight, -contextMenuSettings.VerticalPadding.left, -contextMenuSettings.VerticalPadding.right))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewProfileText, contextMenuSettings.ViewProfileSprite, () => OpenProfilePassportRequested?.Invoke(lastClickedProfileCtx)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ChatText, contextMenuSettings.ChatSprite, () => OpenUserChatRequested?.Invoke(lastClickedProfileCtx)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CallText, contextMenuSettings.CallSprite, () => CallUserRequested?.Invoke(lastClickedProfileCtx)))
                         .AddControl(blockUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BlockText, contextMenuSettings.BlockSprite, () => BlockUserRequested?.Invoke(lastClickedProfileCtx))));
        }

        public void InitializePools()
        {
            contextMenuCts.SafeCancelAndDispose();

            requestsReceivedMembersPool ??= new ObjectPool<MemberListItemView>(
                InstantiateRequestsReceivedMemberPrefab,
                defaultCapacity: REQUESTS_RECEIVED_MEMBER_CARDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: requestsReceivedMemberView => requestsReceivedMemberView.gameObject.SetActive(true),
                actionOnRelease: requestsReceivedMemberView => requestsReceivedMemberView.gameObject.SetActive(false));
        }

        public void SetCommunityId(string id) =>
            currentCommunityId = id;

        public void SetTitle(string title) =>
            communityTitle.text = title;

        public void SetRequestsReceived(int requestsCount) =>
            requestsReceivedText.text = $"{requestsCount} Request{(requestsCount > 1 ? "s" : "")} Received";

        public void ClearRequestReceivedMemberItems()
        {
            foreach (var requestReceivedMember in currentRequestReceivedMembers)
                requestsReceivedMembersPool!.Release(requestReceivedMember);

            currentRequestReceivedMembers.Clear();
        }

        public MemberListItemView[] SetRequestReceivedMemberItems(ICommunityMemberData[] members)
        {
            List<MemberListItemView> result = new List<MemberListItemView>();

            foreach (var community in members)
                result.Add(CreateAndSetupRequestReceivedMembers(community));

            return result.ToArray();
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            this.profileRepositoryWrapper = profileDataProvider;
        }

        public void SetCommunitiesDataProvider(CommunitiesDataProvider.CommunitiesDataProvider dataProvider)
        {
            this.communitiesDataProvider = dataProvider;
        }

        private MemberListItemView InstantiateRequestsReceivedMemberPrefab()
        {
            MemberListItemView requestsReceivedMember = Instantiate(requestReceivedMemberPrefab, requestReceivedMembersGridContainer);
            return requestsReceivedMember;
        }

        private MemberListItemView CreateAndSetupRequestReceivedMembers(ICommunityMemberData community)
        {
            MemberListItemView requestReceivedMemberView = requestsReceivedMembersPool!.Get();

            // Setup card data
            requestReceivedMemberView.Configure(community, MembersListView.MemberListSections.REQUESTS, false, profileRepositoryWrapper!);

            // Setup card events
            requestReceivedMemberView.SubscribeToInteractions(member => OpenProfilePassportRequested?.Invoke(member),
                OnContextMenuButtonClicked,
                null!,
                null!,
                (member, intention) => ElementManageRequestClicked?.Invoke(member, intention));

            currentRequestReceivedMembers.Add(requestReceivedMemberView);
            return requestReceivedMemberView;
        }

        private void OnContextMenuButtonClicked(ICommunityMemberData profile, Vector2 buttonPosition, MemberListItemView elementView)
        {
            lastClickedProfileCtx = profile;
            contextMenuCts = contextMenuCts.SafeRestart();
            UserProfileContextMenuControlSettings.FriendshipStatus status = profile.FriendshipStatus.Convert();
            userProfileContextMenuControlSettings!.SetInitialData(profile.ToUserData(), status == UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND ? status : UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED);
            elementView.CanUnHover = false;

            blockUserContextMenuElement!.Enabled = profile.FriendshipStatus != FriendshipStatus.blocked && profile.FriendshipStatus != FriendshipStatus.blocked_by;

            if (invitationButtonHandler == null)
            {
                invitationButtonHandler = new CommunityInvitationContextMenuButtonHandler(communitiesDataProvider!, contextMenuSettings.ElementsSpacing);
                invitationButtonHandler.AddSubmenuControlToContextMenu(contextMenu!, ITEM_CONTEXT_MENU_SUBMENU_OFFSET, contextMenuSettings.InviteToCommunityText, contextMenuSettings.InviteToCommunitySprite);
            }

            invitationButtonHandler.SetUserToInvite(profile.Address);

            ViewDependencies.ContextMenuOpener.OpenContextMenu(
                new GenericContextMenuParameter(contextMenu!, buttonPosition, actionOnHide: () => elementView.CanUnHover = true),
                contextMenuCts.Token);
        }
    }
}
