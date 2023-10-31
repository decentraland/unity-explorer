using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class ExplorePanelView : ViewBase, IView
    {
        [field: SerializeField]
        public TabSelectorView[] TabSelectorViews { get; private set; }

        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public Transform SubPanelTransform { get; private set; }


    }
}
