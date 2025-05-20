using DCL.UI;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        [field: Header("Animators")]
        [field: SerializeField] internal Animator panelAnimator { get; private set; }
        [field: SerializeField] internal Animator headerAnimator { get; private set; }

        [field: Header("Header")]
        [field: SerializeField] internal DropdownView sortByDropdown { get; private set; }
        [field: SerializeField] internal SearchBarView searchBar { get; private set; }

        [field: Header("Side Section")]
        [field: SerializeField] internal Button createCommunityButton { get; private set; }
        [field: SerializeField] internal Button viewAllMyCommunitiesButton { get; private set; }
        [field: SerializeField] internal LoopListView2 myCommunitiesLoopList { get; private set; }

        [field: Header("Results Section")]
        [field: SerializeField] internal GameObject communityCardPrefab { get; private set; }
        [field: SerializeField] internal Button resultsBackButton { get; private set; }
        [field: SerializeField] internal TMP_Text resultsTitleText { get; private set; }
        [field: SerializeField] internal LoopGridView resultLoopGrid { get; private set; }

        public void SetResultsBackButtonVisible(bool isVisible) =>
            resultsBackButton.gameObject.SetActive(isVisible);

        public void SetResultsTitleText(string text) =>
            resultsTitleText.text = text;
    }
}
