using Cysharp.Threading.Tasks;
using DCL.UI.Controls.Configs;
using MVC;
using System;
using UnityEngine;

namespace DCL.UI
{
    public class CommunityContextMenuController
    {
        private readonly IMVCManager mvcManager;
        private readonly string viewCommunityText;
        private readonly Sprite viewCommunityIcon;
        private readonly Action onViewCommunityClicked;

        public CommunityContextMenuController(
            IMVCManager mvcManager,
            string viewCommunityText,
            Sprite viewCommunityIcon,
            Action onViewCommunityClicked)
        {
            this.mvcManager = mvcManager;
            this.viewCommunityText = viewCommunityText;
            this.viewCommunityIcon = viewCommunityIcon;
            this.onViewCommunityClicked = onViewCommunityClicked;
        }

        public async UniTask ShowContextMenuAsync(Vector3 position, UniTask closeMenuTask, Action onContextMenuHide)
        {
            var contextMenuConfig = new GenericContextMenu()
                .AddControl(new ButtonContextMenuControlSettings(
                    viewCommunityText,
                    viewCommunityIcon,
                    onViewCommunityClicked));

            var showParameter = new GenericContextMenuParameter(
                contextMenuConfig,
                position,
                actionOnHide: onContextMenuHide,
                closeTask: closeMenuTask
            );

            await mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(showParameter));
        }
    }
}
