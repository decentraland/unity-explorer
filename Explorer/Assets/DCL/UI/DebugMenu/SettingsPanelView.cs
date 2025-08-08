using System;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    public class SettingsPanelView: DebugPanelView
    {
        public SettingsPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
        }
    }
}
