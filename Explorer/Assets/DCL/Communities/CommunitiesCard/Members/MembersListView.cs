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
        private const int ELEMENT_MISSING_THRESHOLD = 5;

        [field: SerializeField] public LoopGridView LoopGrid { get; private set; }
        [field: SerializeField] public ScrollRect LoopListScrollRect { get; private set; }
        [field: SerializeField] public CommunityMemberListContextMenuConfiguration ContextMenuSettings { get; private set; }
        [field: SerializeField] public RectTransform SectionButtons { get; private set; }
        [field: SerializeField] public RectTransform ScrollViewRect { get; private set; }
        [field: SerializeField] public MemberListSectionMapping[] MemberListSectionsElements { get; private set; }

        [field: Header("Assets")]
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
            LoopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = ScrollViewRect.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + SectionButtons.sizeDelta.y;

            foreach (var sectionMapping in MemberListSectionsElements)
                sectionMapping.Button.onClick.AddListener(() => ToggleSection(sectionMapping.Section));
        }

        public void SetActive(bool active) => gameObject.SetActive(active);

        private void ToggleSection(MemberListSections section)
        {
            foreach (var sectionMapping in MemberListSectionsElements)
            {
                sectionMapping.SelectedBackground.SetActive(sectionMapping.Section == section);
                sectionMapping.SelectedText.SetActive(sectionMapping.Section == section);
                sectionMapping.UnselectedBackground.SetActive(sectionMapping.Section != section);
                sectionMapping.UnselectedText.SetActive(sectionMapping.Section != section);
            }

            if (currentSection != section)
            {
                currentSection = section;
                ActiveSectionChanged?.Invoke(section);
            }

            currentSection = section;
        }

        public void SetSectionButtonsActive(bool isActive)
        {
            SectionButtons.gameObject.SetActive(isActive);
            ScrollViewRect.sizeDelta = new Vector2(ScrollViewRect.sizeDelta.x, isActive ? scrollViewHeight : scrollViewMaxHeight);
        }

        public void InitGrid(Func<SectionFetchData> currentSectionDataFunc)
        {
            LoopGrid.InitGridView(0, GetLoopGridItemByIndex);
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
            LoopGrid.SetListItemCount(getCurrentSectionFetchData().members.Count, false);
            LoopGrid.RefreshAllShownItem();
        }

        public enum MemberListSections
        {
            ALL,
            BANNED
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
