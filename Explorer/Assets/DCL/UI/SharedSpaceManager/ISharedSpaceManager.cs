using Cysharp.Threading.Tasks;

namespace DCL.UI.SharedSpaceManager
{
    // TODO: Rename to ISharedUISpaceManager
    public interface ISharedSpaceManager
    {
        UniTask ShowAsync(PanelsSharingSpace panel, object parameters = null);
        UniTask HideAsync(PanelsSharingSpace panel, object parameters = null);
        UniTask ToggleVisibilityAsync(PanelsSharingSpace panel, object parameters = null);
        void RegisterPanel(PanelsSharingSpace panel, IPanelInSharedSpace controller);
    }
}
