namespace DCL.UI.GenericContextMenu.Controls
{
    public interface IGenericContextMenuComponent
    {
        void Configure(ContextMenuControlSettings settings);
        void UnregisterListeners();
    }
}
