using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunityCreation
{
    public class CommunityCreationEditionView : ViewBase, IView
    {
        [field: SerializeField] public Button BackgroundCloseButton { get; private set; }
    }
}
