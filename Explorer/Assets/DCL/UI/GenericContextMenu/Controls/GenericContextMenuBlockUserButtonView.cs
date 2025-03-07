using System;
using UnityEngine.Events;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuBlockUserButtonView : GenericContextMenuButtonWithProfileView
    {
        public override void RegisterCloseListener(Action listener) =>
            ButtonComponent.onClick.AddListener(new UnityAction(listener));
    }
}
