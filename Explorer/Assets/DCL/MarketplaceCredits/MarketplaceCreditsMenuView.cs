using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuView : ViewBaseWithAnimationElement
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;
    }
}
