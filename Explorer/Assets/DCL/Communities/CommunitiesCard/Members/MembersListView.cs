using Cysharp.Threading.Tasks;
using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;
using MVC;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListView : MonoBehaviour, IViewWithGlobalDependencies
    {
        public enum MemberListSections
        {
            ALL,
            BANNED
        }

        private const int ELEMENT_MISSING_THRESHOLD = 5;
        private const string KICK_MEMBER_TEXT_FORMAT = "Are you sure you want to kick '{0}' from {1}?";
        private const string BAN_MEMBER_TEXT_FORMAT = "Are you sure you want to ban '{0}' from {1}?";
        private const string KICK_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string KICK_MEMBER_CONFIRM_TEXT = "KICK";
        private const string BAN_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string BAN_MEMBER_CONFIRM_TEXT = "BAN";

        [field: SerializeField] private ConfirmationDialogView confirmationDialogView { get; set; }
        [field: SerializeField] private LoopGridView loopGrid { get; set; }
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; }
        [field: SerializeField] private RectTransform sectionButtons { get; set; }
        [field: SerializeField] private RectTransform scrollViewRect { get; set; }
        [field: SerializeField] private MemberListSectionMapping[] memberListSectionsElements { get; set; }

        [field: Header("Assets")]
        [field: SerializeField] public CommunityMemberListContextMenuConfiguration ContextMenuSettings { get; private set; }
        [field: SerializeField] private Sprite kickSprite { get; set; }
        [field: SerializeField] private Sprite banSprite { get; set; }

        public event Action<MemberListSections> ActiveSectionChanged;
        public event Action? NewDataRequested;
        public event Action<MemberData>? ElementMainButtonClicked;
        public event Action<MemberData, Vector2, MemberListItemView>? ElementContextMenuButtonClicked;
        public event Action<MemberData>? ElementFriendButtonClicked;
        public event Action<MemberData>? ElementUnbanButtonClicked;

        public event Action<MemberData>? KickUserRequested;
        public event Action<MemberData>? BanUserRequested;

        private float scrollViewMaxHeight;
        private float scrollViewHeight;
        private MemberListSections currentSection;
        private ViewDependencies viewDependencies;
        private Func<SectionFetchData> getCurrentSectionFetchData;
        private CancellationTokenSource confirmationDialogCts = new ();

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = scrollViewRect.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + sectionButtons.sizeDelta.y;

            foreach (var sectionMapping in memberListSectionsElements)
                sectionMapping.Button.onClick.AddListener(() => ToggleSection(sectionMapping.Section));
        }

        private void OnDisable()
        {
            confirmationDialogCts.SafeCancelAndDispose();
        }

        internal void ShowKickConfirmationDialog(MemberData profile, string communityName)
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowKickConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowKickConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(KICK_MEMBER_TEXT_FORMAT, profile.name, communityName),
                        KICK_MEMBER_CANCEL_TEXT,
                        KICK_MEMBER_CONFIRM_TEXT,
                        kickSprite,
                        false, false),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                KickUserRequested?.Invoke(profile);
            }
        }

        internal void ShowBanConfirmationDialog(MemberData profile, string communityName)
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowKickConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowKickConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(BAN_MEMBER_TEXT_FORMAT, profile.name, communityName),
                        BAN_MEMBER_CANCEL_TEXT,
                        BAN_MEMBER_CONFIRM_TEXT,
                        banSprite,
                        false, false),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                BanUserRequested?.Invoke(profile);
            }
        }

        public void SetActive(bool active) => gameObject.SetActive(active);

        private void ToggleSection(MemberListSections section)
        {
            if (currentSection == section) return;

            foreach (var sectionMapping in memberListSectionsElements)
            {
                sectionMapping.SelectedBackground.SetActive(sectionMapping.Section == section);
                sectionMapping.SelectedText.SetActive(sectionMapping.Section == section);
                sectionMapping.UnselectedBackground.SetActive(sectionMapping.Section != section);
                sectionMapping.UnselectedText.SetActive(sectionMapping.Section != section);
            }

            currentSection = section;
            ActiveSectionChanged?.Invoke(section);
        }

        public void SetSectionButtonsActive(bool isActive)
        {
            sectionButtons.gameObject.SetActive(isActive);
            scrollViewRect.sizeDelta = new Vector2(scrollViewRect.sizeDelta.x, isActive ? scrollViewHeight : scrollViewMaxHeight);
        }

        public void InitGrid(Func<SectionFetchData> currentSectionDataFunc)
        {
            loopGrid.InitGridView(0, GetLoopGridItemByIndex);
            getCurrentSectionFetchData = currentSectionDataFunc;
        }

        private LoopGridViewItem GetLoopGridItemByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem listItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            MemberListItemView elementView = listItem.GetComponent<MemberListItemView>();

            SectionFetchData membersData = getCurrentSectionFetchData();

            elementView.InjectDependencies(viewDependencies);
            elementView.Configure(membersData.members[index], currentSection);

            elementView.SubscribeToInteractions(member => ElementMainButtonClicked?.Invoke(member),
                (member, position, item) => ElementContextMenuButtonClicked?.Invoke(member, position, item),
                member => ElementFriendButtonClicked?.Invoke(member),
                member => ElementUnbanButtonClicked?.Invoke(member));

            if (index >= membersData.totalFetched - ELEMENT_MISSING_THRESHOLD && membersData.totalFetched < membersData.totalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        public void RefreshGrid()
        {
            loopGrid.SetListItemCount(getCurrentSectionFetchData().members.Count, false);
            loopGrid.RefreshAllShownItem();
        }

        [Serializable]
        public struct MemberListSectionMapping
        {
            [field: SerializeField]
            public MemberListSections Section { get; private set; }

            [field: SerializeField]
            public Button Button { get; private set; }

            [field: SerializeField]
            public GameObject SelectedBackground { get; private set; }

            [field: SerializeField]
            public GameObject SelectedText { get; private set; }

            [field: SerializeField]
            public GameObject UnselectedBackground { get; private set; }

            [field: SerializeField]
            public GameObject UnselectedText { get; private set; }
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }
    }
}
