using DCL.UI.GenericContextMenu.Controls;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UI.GenericContextMenu
{
    public struct GenericContextMenuParameter
    {
        public readonly GenericContextMenuConfig Config;
        public readonly Dictionary<int, Delegate> ControlsActions;
        public readonly Vector2 AnchorPosition;

        public GenericContextMenuParameter(GenericContextMenuConfig config, Dictionary<int, Delegate> controlsActions, Vector2 anchorPosition)
        {
            this.Config = config;
            this.ControlsActions = controlsActions;
            this.AnchorPosition = anchorPosition;
        }
    }
}
