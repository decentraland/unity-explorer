using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using MVC;
using System;
using UnityEngine;

namespace DCL.Chat.Services
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