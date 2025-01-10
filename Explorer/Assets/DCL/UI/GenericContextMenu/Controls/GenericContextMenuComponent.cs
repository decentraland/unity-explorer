using DCL.UI.GenericContextMenu.Controls.Configs;

namespace DCL.UI.GenericContextMenu.Controls
{
    public abstract class GenericContextMenuComponent<T> : GenericContextMenuComponentBase where T : ContextMenuControlSettings
    {
        public abstract void Configure(T settings, object initialValue);
    }
}
