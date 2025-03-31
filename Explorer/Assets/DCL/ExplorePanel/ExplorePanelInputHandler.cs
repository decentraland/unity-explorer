using DCL.ExplorePanel.Components;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace DCL.ExplorePanel
{
    public class ExplorePanelInputHandler : IDisposable, IExplorePanelEscapeAction
    {
        private readonly DCLInput dclInput;
        private readonly LinkedList<Action<InputAction.CallbackContext>> escapeActions = new ();

        public ExplorePanelInputHandler(DCLInput dclInput)
        {
            this.dclInput = dclInput;
        }

        public void RegisterEscapeAction(Action<InputAction.CallbackContext> action)
        {
            if (escapeActions.Count > 0)
                dclInput.UI.Close.performed -= escapeActions.Last.Value;

            dclInput.UI.Close.performed += action;

            if (!escapeActions.Contains(action))
                escapeActions.AddLast(action);
        }

        public void RemoveEscapeAction(Action<InputAction.CallbackContext> action)
        {
            if (!escapeActions.Contains(action)) return;

            dclInput.UI.Close.performed -= action;
            escapeActions.Remove(action);

            if (escapeActions.Count > 0)
                dclInput.UI.Close.performed += escapeActions.Last.Value;
        }

        public void Dispose() =>
            UnregisterHotkeys();

        private void UnregisterHotkeys()
        {
            foreach (var escapeAction in escapeActions)
                dclInput.UI.Close.performed -= escapeAction;
            escapeActions.Clear();
        }
    }
}
