using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.RewardPanel
{
    public class RewardPanelView : ViewBase, IView
    {
        [field: SerializeField]
        public Button ContinueButton { get; private set; }
    }
}
