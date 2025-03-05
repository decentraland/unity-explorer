using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.UI
{
    /// <summary>
    /// Create the area in the container
    /// Share it in the Plugins
    /// When plugins create the controllers, register them
    /// Controllers must implement IControllerInSharedArea
    /// Only one controller will be visible at a time
    /// The previous one hides, but may not allow so the transition does not happen
    /// </summary>
    public class SharedUIArea
    {
        public delegate UniTask ShowControllerDelegate(object? parameters, CancellationToken cancellationToken);
        public delegate UniTask HideControllerDelegate(object? parameters, CancellationToken cancellationToken);

        private struct ControllerData
        {
            public IControllerInSharedArea Controller;
            public ShowControllerDelegate ShowControllerAction;
            public HideControllerDelegate HideControllerAction;
        }

        private readonly Dictionary<string, ControllerData> controllers = new ();
        private CancellationTokenSource cancellationToken = new ();
        private string currentControllerName = string.Empty;

        private bool isTransitioning;
        public string CurrentController => currentControllerName;

        public void RegisterController(string controllerName, IControllerInSharedArea controller, ShowControllerDelegate showControllerAction, HideControllerDelegate hideControllerAction)
        {
            controllers.Add(controllerName, new ControllerData(){Controller = controller,
                                                                 ShowControllerAction = showControllerAction,
                                                                 HideControllerAction = hideControllerAction});
        }

        public void UnregisterController(string controllerName)
        {
            controllers.Remove(controllerName);
        }

        public async UniTask ShowControllerAsync(string controllerName, object? parameters)
        {
            Debug.LogError(">>>>> SHOWING");

            if(isTransitioning)
                return;

            isTransitioning = true;

            bool isTransitionAllowed = true;

            if (!string.IsNullOrEmpty(currentControllerName) &&
                currentControllerName != controllerName &&
                controllers[currentControllerName].Controller.State != ControllerState.ViewHidden)
            {
                isTransitionAllowed = await controllers[currentControllerName].Controller.HidingRequestedAsync(parameters, cancellationToken.SafeRestart().Token);

                if(isTransitionAllowed)
                    currentControllerName = string.Empty;
            }

            if (isTransitionAllowed)
            {
                if (!controllers.TryGetValue(controllerName, out ControllerData controllerData))
                    Debug.LogError("There is no view registered with name: " + controllerName);
                else
                {
                    try
                    {
                        controllerData.ShowControllerAction(parameters, cancellationToken.SafeRestart().Token).Forget();
                        currentControllerName = controllerName;
                    }
                    catch (Exception e)
                    {
                        isTransitioning = false;
                        throw;
                    }
                }
            }

            isTransitioning = false;
        }

        public async UniTask HideControllerAsync(string controllerName, object parameters)
        {
            Debug.LogError(">>>>> HIDING");

            if(isTransitioning)
                return;

            isTransitioning = true;

            if (!controllers.TryGetValue(controllerName, out ControllerData controllerData))
                    Debug.LogError("There is no view registered with name: " + controllerName);
            else if (controllerName == currentControllerName && controllers[currentControllerName].Controller.State != ControllerState.ViewHidden)
                if(await controllerData.Controller.HidingRequestedAsync(parameters, cancellationToken.SafeRestart().Token))
                    currentControllerName = string.Empty;

            isTransitioning = false;
        }
    }
}
