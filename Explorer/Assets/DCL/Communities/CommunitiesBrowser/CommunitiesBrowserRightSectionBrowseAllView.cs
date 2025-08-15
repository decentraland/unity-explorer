using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{

    public class CommController
    {

        private void ViewAllMyCommunitiesResults()
        {
            ClearSearchBar();
            //Setup results view with my communities data?
            view.SetResultsBackButtonVisible(true);
            view.SetResultsTitleText(MY_COMMUNITIES_RESULTS_TITLE);

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(
                name: string.Empty,
                onlyMemberOf: true,
                pageNumber: 1,
                elementsPerPage: COMMUNITIES_PER_PAGE,
                ct: loadResultsCts.Token).Forget();

        }
    }


    public class CommunitiesBrowserRightSectionBrowseAllView : MonoBehaviour
    {
        [field: SerializeField] public ScrollRect ScrollRect { get; private set; }

        // Setup Streaming Communities
        // Setup Rest of Communities

        //Update upon changes on community state

    }
}
