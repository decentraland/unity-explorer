using DCL.UI.GenericContextMenu.Controls.Configs;
using System;

namespace DCL.UI.GenericContextMenu.Controls
{
    public interface IGenericContextMenuComponent
    {
        void Configure(ContextMenuControlSettings settings, object initialValue);
        void UnregisterListeners();
        void RegisterListener(Delegate listener);
        void RegisterCloseListener(Action listener);
    }
}
