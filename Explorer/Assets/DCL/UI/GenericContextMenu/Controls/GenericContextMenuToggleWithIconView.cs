using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuToggleWithIconView : GenericContextMenuToggleView
    {
        [field: SerializeField] public Image ImageComponent { get; private set; }

        public void Configure(ToggleWithIconContextMenuControlSettings settings, bool initialValue)
        {
            base.Configure(settings, initialValue);
            ImageComponent.sprite = settings.toggleIcon;
        }

        public override void RegisterCloseListener(Action listener) {}
    }
}
