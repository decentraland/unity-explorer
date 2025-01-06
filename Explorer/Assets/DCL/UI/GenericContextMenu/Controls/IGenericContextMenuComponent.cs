using DCL.UI.GenericContextMenu.Controls.Configs;

namespace DCL.UI.GenericContextMenu.Controls
{
    public interface IGenericContextMenuComponent
    {
        void Configure(ContextMenuControlSettings settings);
        void UnregisterListeners();
    }
}
