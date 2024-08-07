using Cysharp.Threading.Tasks;
using DCL.ExplorePanel;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.UI.Sidebar
{
    public class ProfileMenuView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] public ProfileWidgetView ProfileMenuWidget { get; private set; }
        [field: SerializeField] public SystemMenuView SystemMenuView { get; private set; }
    }
}
