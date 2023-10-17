using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MVC
{
    public class MVCManager : IMVCManager
    {
        private readonly Dictionary<Type, IController> controllers;
        private readonly IWindowsStackManager windowsStackManager;
        private readonly CancellationTokenSource destructionCancellationTokenSource;

        public MVCManager(IWindowsStackManager windowsStackManager, CancellationTokenSource destructionCancellationTokenSource)
        {
            this.windowsStackManager = windowsStackManager;
            this.destructionCancellationTokenSource = destructionCancellationTokenSource;

            controllers = new Dictionary<Type, IController>(200);
        }

        /// <summary>
        ///     Instead of a builder just expose a method
        ///     to add controller gradually (from different plug-ins).
        ///     It should not be exposed to the interface as the interface is for consumers only
        /// </summary>
        public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: MonoBehaviour, IView
        {
            // throw an exception if the same combination of <TView, TInputData> was already added
            controllers.Add(typeof(IController<TView, TInputData>), controller);
        }

        public async UniTask Show<TView, TInputData>(ShowCommand<TView, TInputData> command) where TView: MonoBehaviour, IView
        {
            // Find the controller
            IController controller = controllers[typeof(IController<TView, TInputData>)];

            (IController previousController, CanvasOrdering newControllerOrdering) = windowsStackManager.Push(controller);

            previousController.OnBlur();

            await command.Execute(controller, newControllerOrdering, destructionCancellationTokenSource.Token);

            // "Close" command has been received

            await controller.HideView(destructionCancellationTokenSource.Token);

            // Pop the stack

            IController poppedController = windowsStackManager.Pop(controller);
            poppedController.OnFocus();
        }
    }
}
