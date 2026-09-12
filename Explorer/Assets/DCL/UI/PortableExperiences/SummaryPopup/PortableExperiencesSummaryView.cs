using MVC;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.PortableExperiences.SummaryPopup
{
    public class PortableExperiencesSummaryView : ViewBase, IView
    {
        [field: SerializeField]
        internal Button closeButton = null!;

        [field: SerializeField]
        internal LoopListView2 globalPxLoopList = null!;

        [field: SerializeField]
        internal LoopListView2 smartWearableLoopList = null!;

        [field: SerializeField]
        internal LoopListView2 localPxLoopList = null!;
    }
}
