using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using DCL.UI;
using DCL.Utilities;
using ECS.Abstract;
using MVC;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.InputSystem;
using Utility.UIToolkit;
using Utility;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private enum UiVisibility
        {
            AllVisible,
            ExplorerHidden,
            AllHidden,
        }

        private const int TOAST_DURATION_MS = 3000;
        private const string EXPLORER_HIDDEN_TOAST = "Press   <link=key>U</link>   to hide the scene UI.";
        private const string ALL_HIDDEN_TOAST = "Press   <link=key>U</link>   to show the UI again.";

        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;
        private readonly IReactiveProperty<bool> currentSceneUiOnly;

        private SingleInstanceEntity camera;
        private CancellationTokenSource? toastCt;
        private readonly WarningNotificationView warningNotificationView;

        private UiVisibility visibility = UiVisibility.AllVisible;

        private readonly IReadOnlyList<InputAction> actionsThatEnableUI;

        internal UpdateShowHideUIInputSystem(World world, IMVCManager mvcManager, WarningNotificationView warningNotificationView, IReactiveProperty<bool> currentSceneUiOnly) : base(world)
        {
            dclInput = DCLInput.Instance;
            actionsThatEnableUI = new List<InputAction>
            {
                dclInput.Shortcuts.Communities,
                dclInput.Shortcuts.Map,
                dclInput.Shortcuts.Backpack,
                dclInput.Shortcuts.CameraReel,
                dclInput.Shortcuts.Settings,
                dclInput.Shortcuts.Controls,
                dclInput.Shortcuts.MainMenu,
                dclInput.UI.Submit,
            };

            this.mvcManager = mvcManager;
            this.warningNotificationView = warningNotificationView;
            this.currentSceneUiOnly = currentSceneUiOnly;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            // TODO: Should this really be in a system? It's not really updating anything, just checking for input triggers

            UiVisibility? fromInputs = CheckInputs();

            if (fromInputs.HasValue)
            {
                visibility = fromInputs.Value;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(visibility == UiVisibility.AllVisible);

                ShowOrHideToast();
            }
            else if (World.TryGet(camera, out ToggleUIRequest request))
            {
                visibility = request.Enable ? UiVisibility.AllVisible : UiVisibility.AllHidden;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(request.Except, visibility == UiVisibility.AllVisible);

                World.Remove<ToggleUIRequest>(camera);

                ShowOrHideToast();
            }

            currentSceneUiOnly.UpdateValue(visibility == UiVisibility.ExplorerHidden);

            foreach (UIDocumentTracker tracker in UIDocumentTracker.ActiveDocuments)
            {
                if (!tracker.CanBeHidden)
                    continue;

                bool visible = tracker.IsSceneUi
                    ? visibility != UiVisibility.AllHidden
                    : visibility == UiVisibility.AllVisible;

                tracker.Document.rootVisualElement.SetVisible(visible);
            }
        }

        private UiVisibility? CheckInputs()
        {
            if (dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
                return visibility switch
                {
                    UiVisibility.AllVisible => UiVisibility.ExplorerHidden,
                    UiVisibility.ExplorerHidden => UiVisibility.AllHidden,
                    _ => UiVisibility.AllVisible,
                };

            foreach (InputAction inputAction in actionsThatEnableUI)
            {
                if (inputAction.WasPressedThisFrame())
                    return UiVisibility.AllVisible;
            }

            return null;
        }

        private void ShowOrHideToast()
        {
            if (visibility == UiVisibility.AllVisible)
            {
                toastCt.SafeCancelAndDispose();
                warningNotificationView.Hide();
                return;
            }

            string text = visibility == UiVisibility.ExplorerHidden ? EXPLORER_HIDDEN_TOAST : ALL_HIDDEN_TOAST;

            toastCt = toastCt.SafeRestart();

            warningNotificationView.AnimatedShowAsync(text, TOAST_DURATION_MS, toastCt.Token)
                                   .SuppressCancellationThrow()
                                   .Forget();
        }
    }
}
