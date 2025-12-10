using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.FeatureFlags;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.UI.Sidebar
{
    public class SidebarPanelsShortcutsHandler : IDisposable
    {
        private readonly DCLInput dclInput;
        private readonly World world;

        private float lastQuickEmoteTime;
        private bool isCommunitiesFeatureEnabled;

        public SidebarPanelsShortcutsHandler(
            IMVCManager mvcManager,
            DCLInput dclInput,
            EmotesBus emotesBus,
            World world)
        {
            this.dclInput = dclInput;
            this.world = world;

            ConfigureShortcutsAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid ConfigureShortcutsAsync(CancellationToken ct)
        {
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.FRIENDS))

            dclInput.Shortcuts.Controls.performed += OnInputShortcutsControlsPanelPerformedAsync;
            dclInput.UI.Submit.performed += OnUISubmitPerformedAsync;

        }

        private async void OnUISubmitPerformedAsync(InputAction.CallbackContext obj)
        {
            //if (IsRegistered(PanelsSharingSpace.Chat) && !isExplorePanelVisible && !isChatBlockerVisible)
            //  await ShowAsync(PanelsSharingSpace.Chat, new ChatMainSharedAreaControllerShowParams(true, true));
        }


        private async void OnInputShortcutsControlsPanelPerformedAsync(InputAction.CallbackContext obj)
        {
            //mvcManager.ToggleAsync(ControlsPanelController.IssueCommand()).Forget();
        }


        public void Dispose()
        {
        }
    }
}
