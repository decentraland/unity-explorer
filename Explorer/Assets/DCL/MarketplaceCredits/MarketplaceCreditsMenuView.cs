using MVC;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.MarketplaceCredits
{
    public class MarketplaceCreditsMenuView : ViewBaseWithAnimationElement
    {
        [field: SerializeField]
        public List<Button> CloseButtons { get; private set; } = null!;
    }
}
