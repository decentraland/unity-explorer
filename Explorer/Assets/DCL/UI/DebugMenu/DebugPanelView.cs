using System;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    public abstract class DebugPanelView
    {
        private const string USS_DEBUG_PANEL_HIDDEN = "debug-panel--hidden";
        private const string USS_SIDEBAR_BUTTON_SELECTED = "sidebar__button--selected";

        public bool Visible { get; private set; }

        private bool shownOnce;

        private readonly VisualElement root;
        private readonly Button sidebarButton;

        protected DebugPanelView(VisualElement root, Button sidebarButton, Action closeClicked)
        {
            this.root = root;
            this.sidebarButton = sidebarButton;

            root.EnableInClassList(USS_DEBUG_PANEL_HIDDEN, true);
            root.style.display = DisplayStyle.None;

            root.Q<Button>("CloseButton").clicked += closeClicked;
        }

        public virtual void Toggle()
        {
            Visible = !Visible;

            if (!shownOnce && Visible)
            {
                // We use this (plus setting display to None in OnEnable) to force UI Toolkit
                // to redraw all the items on the first open. Without it some styles are not applied.
                root.style.display = DisplayStyle.Flex;
                shownOnce = true;
            }

            sidebarButton.EnableInClassList(USS_SIDEBAR_BUTTON_SELECTED, Visible);
            root.EnableInClassList(USS_DEBUG_PANEL_HIDDEN, !Visible);
        }
    }
}
