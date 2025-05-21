using DCL.UI;
using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;
using System;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListView : MonoBehaviour
    {
        [field: SerializeField] public LoopListView2 LoopList { get; private set; }
        [field: SerializeField] public CommunityMemberListContextMenuConfiguration ContextMenuSettings { get; private set; }
        [field: SerializeField] public RectTransform SectionButtons { get; private set; }
        [field: SerializeField] public RectTransform ScrollViewRect { get; private set; }
        [field: SerializeField] public MemberListSectionMapping[] MemberListSectionsElements { get; private set; }

        public event Action<MemberListSections> ActiveSectionChanged;

        private float scrollViewMaxHeight;
        private float scrollViewHeight;
        private MemberListSections currentSection;

        private void Awake()
        {
            LoopList.ScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = SectionButtons.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + SectionButtons.sizeDelta.y;

            foreach (var sectionMapping in MemberListSectionsElements)
                sectionMapping.Button.onClick.AddListener(() => ToggleSection(sectionMapping.Section));
        }

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
                ActiveSectionChanged?.Invoke(section);

            currentSection = section;
        }

        public void SetSectionButtonsActive(bool isActive)
        {
            SectionButtons.gameObject.SetActive(isActive);
            ScrollViewRect.sizeDelta = new Vector2(ScrollViewRect.sizeDelta.x, isActive ? scrollViewHeight : scrollViewMaxHeight);
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
    }
}
