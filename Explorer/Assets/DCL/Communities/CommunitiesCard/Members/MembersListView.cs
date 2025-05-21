using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListView : MonoBehaviour
    {
        [field: SerializeField] public LoopListView2 LoopList { get; private set; }
        [field: SerializeField] public CommunityMemberListContextMenuConfiguration ContextMenuSettings { get; private set; }
        [field: SerializeField] public RectTransform SectionButtons { get; private set; }
        [field: SerializeField] public RectTransform ScrollViewRect { get; private set; }

        private float scrollViewMaxHeight;
        private float scrollViewHeight;

        private void Awake()
        {
            LoopList.ScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = SectionButtons.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + SectionButtons.sizeDelta.y;
        }

        public void SetSectionButtonsActive(bool isActive)
        {
            SectionButtons.gameObject.SetActive(isActive);
            ScrollViewRect.sizeDelta = new Vector2(ScrollViewRect.sizeDelta.x, isActive ? scrollViewHeight : scrollViewMaxHeight);
        }
    }
}
