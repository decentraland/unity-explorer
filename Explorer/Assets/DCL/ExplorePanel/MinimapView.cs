using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class MinimapView : ViewBase, IView
    {
        [field: SerializeField]
        public Button OpenExploreMapButton { get; private set; }
    }
}
