using Cysharp.Threading.Tasks;
using MVC;

namespace DCL.UI.SharedSpaceManager
{
    public interface ISharedSpaceManager
    {
        UniTask ShowAsync(PanelsSharingSpace panel, object parameters);
        UniTask HideAsync(PanelsSharingSpace panel, object parameters);
        UniTask ToggleVisibilityAsync(PanelsSharingSpace panel, object parameters);
        void RegisterPanelController(PanelsSharingSpace panel, IController controller);
    }
}
