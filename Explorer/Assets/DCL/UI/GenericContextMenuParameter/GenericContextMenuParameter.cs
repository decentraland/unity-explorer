using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

namespace DCL.UI.GenericContextMenuParameter
{
    public struct GenericContextMenuParameter
    {
        public readonly GenericContextMenu Config;
        public readonly Vector2 AnchorPosition;
        public readonly Rect? OverlapRect;
        public readonly Action? ActionOnShow;
        public readonly Action? ActionOnHide;
        public readonly UniTask? CloseTask;

        public GenericContextMenuParameter(GenericContextMenu config,
            Vector2 anchorPosition,
            Rect? overlapRect = null,
            Action? actionOnShow = null,
            Action? actionOnHide = null,
            UniTask? closeTask = null)
        {
            this.Config = config;
            this.AnchorPosition = anchorPosition;
            this.OverlapRect = overlapRect;
            this.ActionOnShow = actionOnShow;
            this.ActionOnHide = actionOnHide;
            this.CloseTask = closeTask;
        }
    }
}
