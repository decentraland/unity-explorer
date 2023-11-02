using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.ExplorePanel
{
    public class PersistentExploreOpenerView : ViewBase, IView
    {
        [field: SerializeField]
        public Button OpenExploreButton { get; private set; }
    }
}
