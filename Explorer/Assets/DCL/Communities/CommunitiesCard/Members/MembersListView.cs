using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;
using MVC;
using System;
using UnityEngine.UI;
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

        [field: SerializeField] private LoopGridView loopGrid { get; set; }
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; }
        [field: SerializeField] private RectTransform sectionButtons { get; set; }
        [field: SerializeField] private RectTransform scrollViewRect { get; set; }
        [field: SerializeField] private MemberListSectionMapping[] memberListSectionsElements { get; set; }

        [field: Header("Assets")]
        [field: SerializeField] public CommunityMemberListContextMenuConfiguration ContextMenuSettings { get; private set; }
        [field: SerializeField] public Sprite KickSprite { get; private set; }
        [field: SerializeField] public Sprite BanSprite { get; private set; }

        public event Action<MemberListSections> ActiveSectionChanged;
        public event Action? NewDataRequested;
        public event Action<MemberData>? ElementMainButtonClicked;
        public event Action<MemberData, Vector2, MemberListItemView>? ElementContextMenuButtonClicked;
        public event Action<MemberData>? ElementFriendButtonClicked;
        public event Action<MemberData>? ElementUnbanButtonClicked;

        private float scrollViewMaxHeight;
        private float scrollViewHeight;
        private MemberListSections currentSection;
        private ViewDependencies viewDependencies;
        private Func<SectionFetchData> getCurrentSectionFetchData;

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = scrollViewRect.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + sectionButtons.sizeDelta.y;

            foreach (var sectionMapping in memberListSectionsElements)
                sectionMapping.Button.onClick.AddListener(() => ToggleSection(sectionMapping.Section));
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
