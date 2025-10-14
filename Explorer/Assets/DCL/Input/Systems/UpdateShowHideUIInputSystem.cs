using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.InWorldCamera;
using DCL.UI;
using ECS.Abstract;
using MVC;
using System.Threading;
using UnityEngine.UIElements;
using Utility.UIToolkit;
using Utility;

namespace DCL.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class UpdateShowHideUIInputSystem : BaseUnityLoopSystem
    {
        private const int TOAST_DURATION_MS = 3000;

        private readonly DCLInput dclInput;
        private readonly IMVCManager mvcManager;

        private SingleInstanceEntity camera;
        private CancellationTokenSource? toastCt;
        private readonly WarningNotificationView warningNotificationView;

        private bool currentUIVisibilityState = true;

        private UpdateShowHideUIInputSystem(World world, IMVCManager mvcManager, WarningNotificationView warningNotificationView) : base(world)
        {
            dclInput = DCLInput.Instance;
            this.mvcManager = mvcManager;
            this.warningNotificationView = warningNotificationView;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            // TODO: Should this really be in a system? It's not really updating anything, just checking for input triggers

            if (dclInput.Shortcuts.ShowHideUI.WasPressedThisFrame())
            {
                currentUIVisibilityState = !currentUIVisibilityState;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(currentUIVisibilityState);

                ShowOrHideToast();
            }
            else if (World.TryGet(camera, out ToggleUIRequest request))
            {
                currentUIVisibilityState = request.Enable;

                // Common UIs
                mvcManager.SetAllViewsCanvasActive(request.Except, currentUIVisibilityState);

                World.Remove<ToggleUIRequest>(camera);

                ShowOrHideToast();
            }

            foreach (UIDocument doc in UIDocumentTracker.ActiveDocuments)
                doc.rootVisualElement.SetDisplayed(currentUIVisibilityState);
        }

        private void ShowOrHideToast()
        {
            if (!currentUIVisibilityState)
            {
                toastCt = toastCt.SafeRestart();

                warningNotificationView.AnimatedShowAsync(TOAST_DURATION_MS, toastCt.Token)
                                       .SuppressCancellationThrow()
                                       .Forget();
            }
            else
            {
                toastCt.SafeCancelAndDispose();
                warningNotificationView.Hide();
            }
        }
    }
}
