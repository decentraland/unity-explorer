using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
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
        public readonly Rect? OverlapRect;
        public readonly Action? ActionOnShow;
        public readonly Action? ActionOnHide;
        public readonly UniTask? CloseTask;
        public readonly Dictionary<int, object>? InitialValues;

        public GenericContextMenuParameter(GenericContextMenuConfig config,
            Dictionary<int, Delegate> controlsActions,
            Vector2 anchorPosition,
            Rect? overlapRect = null,
            Action? actionOnShow = null,
            Action? actionOnHide = null,
            UniTask? closeTask = null,
            Dictionary<int, object>? initialValues = null)
        {
            this.Config = config;
            this.ControlsActions = controlsActions;
            this.AnchorPosition = anchorPosition;
            this.OverlapRect = overlapRect;
            this.ActionOnShow = actionOnShow;
            this.ActionOnHide = actionOnHide;
            this.CloseTask = closeTask;
            this.InitialValues = initialValues;
        }
    }
}
